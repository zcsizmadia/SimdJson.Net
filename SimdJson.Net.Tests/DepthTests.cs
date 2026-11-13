using System.Text;

namespace SimdJson.Tests;

public class DepthTests
{
    private static string NestedArrays(int depth) =>
        new string('[', depth) + "1" + new string(']', depth);

    // Opens the root array and then each first element until a scalar, returning how many
    // arrays were opened. Throws whatever the first failing open throws.
    private static int OpenNestedArrays(JsonDocument doc)
    {
        var handles = new List<IDisposable>();
        try
        {
            JsonArray array = doc.GetArray();
            handles.Add(array);
            int opened = 1;
            while (true)
            {
                JsonValue first = array.At(0);
                handles.Add(first);
                if (first.ValueKind != JsonValueKind.Array) return opened;
                array = first.GetArray();
                handles.Add(array);
                opened++;
            }
        }
        finally
        {
            for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
        }
    }

    // ── SimdJsonParser ───────────────────────────────────────────────────────

    [Test]
    public async Task DefaultParser_MaxDepthIs1024()
    {
        using var parser = new SimdJsonParser();
        await Assert.That(parser.MaxDepth).IsEqualTo((nuint)1024);
    }

    [Test]
    public async Task DefaultParser_OpensContainersUpToDepth1023()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse(NestedArrays(1023));
        await Assert.That(OpenNestedArrays(doc)).IsEqualTo(1023);
    }

    [Test]
    public async Task DefaultParser_ContainerAtDepth1024_ThrowsDepthError()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse(NestedArrays(1024));
        var ex = await Assert.That(() => OpenNestedArrays(doc)).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    public async Task Allocate_LowDepth_RejectsDeeperArray()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 3);

        using (var ok = parser.Parse(NestedArrays(2)))
        {
            await Assert.That(OpenNestedArrays(ok)).IsEqualTo(2);
        }

        using var deep = parser.Parse(NestedArrays(3));
        var ex = await Assert.That(() => OpenNestedArrays(deep)).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    public async Task Allocate_LowDepth_RejectsDeeperObject()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 3);
        using var doc = parser.Parse("""{"a":{"b":{"c":1}}}""");
        using var a = doc.GetField("a");
        using var aObj = a.GetObject();
        using var b = aObj.GetField("b");
        var ex = await Assert.That(() => b.GetObject()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    public async Task Allocate_DepthOne_RejectsRootContainer()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 1);
        using var doc = parser.Parse("[1]");
        var ex = await Assert.That(() => doc.GetArray()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    public async Task Allocate_LowDepth_SkippedContainersAreNotChecked()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 2);
        using var doc = parser.Parse("""{"deep":[[[[[1]]]]],"b":5}""");
        using var b = doc.GetField("b");
        await Assert.That(b.GetInt64()).IsEqualTo(5L);
    }

    [Test]
    public async Task Allocate_HighDepth_AllowsDeeperContainers()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 2048);
        using var doc = parser.Parse(NestedArrays(1500));
        await Assert.That(OpenNestedArrays(doc)).IsEqualTo(1500);
    }

    [Test]
    public async Task Allocate_LowerDepthWithinCapacity_KeepsCurrentDepth()
    {
        // simdjson only reallocates, and so only changes max_depth, when the capacity or depth grows.
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 1024);
        parser.Allocate(1024, 8);
        await Assert.That(parser.MaxDepth).IsEqualTo((nuint)1024);

        parser.Allocate(128 * 1024, 8);
        await Assert.That(parser.MaxDepth).IsEqualTo((nuint)8);
    }

    [Test]
    public async Task Allocate_WithLiveDocument_Throws()
    {
        // Reallocating would free the buffers the live document reads from.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse(NestedArrays(5));
        await Assert.That(() => parser.Allocate(128 * 1024, 16)).Throws<InvalidOperationException>();
        await Assert.That(OpenNestedArrays(doc)).IsEqualTo(5);
    }

    // ── NdjsonParser ─────────────────────────────────────────────────────────

    [Test]
    public async Task OpenStream_MaxDepth_RejectsOnlyTheDeepDocument()
    {
        string ndjson = string.Join('\n', NestedArrays(2), NestedArrays(3), NestedArrays(1));
        using var stream = NdjsonParser.OpenStream(Encoding.UTF8.GetBytes(ndjson),
            new NdjsonParserOptions { MaxDepth = 3 });

        var results = new List<int>();
        while (stream.MoveNext())
        {
            try
            {
                results.Add(OpenNestedArrays(stream.Current!));
            }
            catch (SimdJsonException ex) when (ex.ErrorCode == -12)
            {
                results.Add(-12);
            }
        }

        await Assert.That(results).IsEquivalentTo(new[] { 2, -12, 1 });
    }

    [Test]
    public async Task OpenStream_HighMaxDepth_AppliesToEveryBatch()
    {
        // Several documents per batch and many batches, so simdjson's threaded iterate_many swaps
        // parsers between batches. Before simdjson 5.1.1 every other batch used the default depth
        // (simdjson#2996), which the deep wildcard query below would hit.
        const int depth = 1500;
        const int count = 24;
        string doc = NestedArrays(depth);
        string ndjson = string.Join('\n', Enumerable.Repeat(doc, count));
        string path = "$" + string.Concat(Enumerable.Repeat("[0]", depth - 2)) + "[*]";

        using var stream = NdjsonParser.OpenStream(Encoding.UTF8.GetBytes(ndjson),
            new NdjsonParserOptions { MaxDepth = 2048, BatchSize = doc.Length * 2 + 2 });

        int parsed = 0;
        while (stream.MoveNext())
        {
            int matches = 0;
            stream.Current!.ForEachAtPath(path, _ => matches++);
            await Assert.That(matches).IsEqualTo(1);
            parsed++;
        }

        await Assert.That(parsed).IsEqualTo(count);
    }

    [Test]
    public async Task OpenStream_DefaultMaxDepth_DeepWildcardThrows()
    {
        // Control for the test above: the same query fails at the default depth.
        const int depth = 1500;
        string path = "$" + string.Concat(Enumerable.Repeat("[0]", depth - 2)) + "[*]";
        using var stream = NdjsonParser.OpenStream(Encoding.UTF8.GetBytes(NestedArrays(depth)));
        await Assert.That(stream.MoveNext()).IsTrue();
        var ex = await Assert.That(() => stream.Current!.ForEachAtPath(path, _ => { }))
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    public async Task ParseAsync_MaxDepth_SkipsDeepLines()
    {
        string ndjson = string.Join('\n', NestedArrays(2), NestedArrays(4), NestedArrays(3)) + "\n";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
        var depths = new List<int>();
        await foreach (var d in NdjsonParser.ParseAsync(input, OpenNestedArrays,
            new NdjsonParserOptions { MaxDepth = 4 }))
        {
            depths.Add(d);
        }

        await Assert.That(depths).IsEquivalentTo(new[] { 2, 3 });
    }

    [Test]
    public async Task ParseParallelAsync_MaxDepth_ThrowsWhenNotSkipping()
    {
        string ndjson = string.Join('\n', NestedArrays(2), NestedArrays(4)) + "\n";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
        var options = new NdjsonParserOptions { MaxDepth = 4, SkipMalformedLines = false };

        var ex = await Assert.That(async () =>
        {
            await foreach (var _ in NdjsonParser.ParseParallelAsync(input, OpenNestedArrays, options)) { }
        }).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task MaxDepth_NotPositive_Throws(int maxDepth)
    {
        var options = new NdjsonParserOptions { MaxDepth = maxDepth };
        await Assert.That(() => NdjsonParser.OpenStream("[1]"u8, options))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => NdjsonParser.ForEachAsync(new MemoryStream(), _ => { }, options))
            .Throws<ArgumentOutOfRangeException>();
    }
}
