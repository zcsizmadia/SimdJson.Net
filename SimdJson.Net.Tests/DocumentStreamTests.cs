using System.Text;

namespace SimdJson.Tests;

// ─── In-memory NDJSON via simdjson iterate_many ───────────────────────────────

public class DocumentStreamTests
{
    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    private static long SelectId(JsonDocument doc)
    {
        using var v = doc.GetField("id");
        return v.GetInt64();
    }

    [Test]
    public async Task Parse_ProjectsEveryDocumentInOrder()
    {
        var ids = NdjsonParser.Parse(
            Utf8("{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n"), SelectId);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task Parse_NoTrailingNewline_StillReadsLastDocument()
    {
        var ids = NdjsonParser.Parse(Utf8("{\"id\":1}\n{\"id\":2}"), SelectId);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2 });
    }

    [Test]
    public async Task Parse_EmptyInput_ReturnsNothing()
    {
        var ids = NdjsonParser.Parse(ReadOnlySpan<byte>.Empty, SelectId);
        await Assert.That(ids.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_ConcatenatedWithoutNewlines_ReadsEachDocument()
    {
        // iterate_many accepts documents run together, not only newline-delimited ones.
        var ids = NdjsonParser.Parse(Utf8("{\"id\":1}{\"id\":2}{\"id\":3}"), SelectId);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task Parse_ScalarDocuments_AreRead()
    {
        var values = NdjsonParser.Parse(Utf8("1\n2\n3\n"), d => d.GetInt64());
        await Assert.That(values).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task Parse_SkipsMalformedDocument_ByDefault()
    {
        var ids = NdjsonParser.Parse(
            Utf8("{\"id\":1}\n{not json}\n{\"id\":3}\n"), SelectId);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 3 });
    }

    [Test]
    public async Task Parse_MalformedDocument_ThrowsWhenNotSkipping()
    {
        await Assert.That(() => NdjsonParser.Parse(
            Utf8("{\"id\":1}\n{not json}\n"), SelectId,
            new NdjsonParserOptions { SkipMalformedLines = false }))
            .Throws<SimdJsonException>();
    }

    [Test]
    public async Task Parse_MatchesTheStreamingParser()
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= 500; i++)
        {
            sb.Append("{\"id\":").Append(i).Append("}\n");
        }

        var batched = NdjsonParser.Parse(Utf8(sb.ToString()), SelectId);

        var sequential = new List<long>();
        using var stream = new MemoryStream(Utf8(sb.ToString()));
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
        {
            sequential.Add(id);
        }

        await Assert.That(batched).IsEquivalentTo(sequential);
    }

    // ── Comma-separated input ─────────────────────────────────────────────────

    [Test]
    public async Task Parse_CommaSeparated_ReadsArrayElementsAsDocuments()
    {
        var ids = NdjsonParser.Parse(
            Utf8("{\"id\":1},{\"id\":2},{\"id\":3}"), SelectId,
            new NdjsonParserOptions { AllowCommaSeparated = true });
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task Parse_CommaSeparated_RejectedByDefault()
    {
        // Without the option the comma is not a document separator, so the second document
        // is not read. Skipping is on by default, so this reports rather than throws.
        var ids = NdjsonParser.Parse(Utf8("{\"id\":1},{\"id\":2}"), SelectId);
        await Assert.That(ids.Count).IsLessThan(2);
    }

    // ── Batch size ────────────────────────────────────────────────────────────

    [Test]
    public async Task Parse_SmallBatchSize_StillReadsEveryDocument()
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= 300; i++)
        {
            sb.Append("{\"id\":").Append(i).Append("}\n");
        }

        // Batches deliberately far smaller than the input, to cross many boundaries.
        var ids = NdjsonParser.Parse(Utf8(sb.ToString()), SelectId,
            new NdjsonParserOptions { BatchSize = 200 });
        await Assert.That(ids.Count).IsEqualTo(300);
        await Assert.That(ids[0]).IsEqualTo(1L);
        await Assert.That(ids[^1]).IsEqualTo(300L);
    }

    [Test]
    public async Task Parse_NegativeBatchSize_Throws()
    {
        await Assert.That(() => NdjsonParser.Parse(Utf8("{\"id\":1}"), SelectId,
            new NdjsonParserOptions { BatchSize = -1 }))
            .Throws<ArgumentOutOfRangeException>();
    }

    // ── Stream handle ─────────────────────────────────────────────────────────

    [Test]
    public async Task OpenStream_ExposesPerDocumentOffsetAndSource()
    {
        using var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n{\"id\":22}\n"));

        await Assert.That(stream.MoveNext()).IsTrue();
        await Assert.That(stream.CurrentIndex).IsEqualTo((nuint)0);
        await Assert.That(Encoding.UTF8.GetString(stream.CurrentSource).Trim())
            .IsEqualTo("{\"id\":1}");

        await Assert.That(stream.MoveNext()).IsTrue();
        await Assert.That(stream.CurrentIndex).IsEqualTo((nuint)9);
        await Assert.That(Encoding.UTF8.GetString(stream.CurrentSource).Trim())
            .IsEqualTo("{\"id\":22}");

        await Assert.That(stream.MoveNext()).IsFalse();
    }

    [Test]
    public async Task OpenStream_SizeInBytes_MatchesInput()
    {
        byte[] input = Utf8("{\"id\":1}\n{\"id\":2}\n");
        using var stream = NdjsonParser.OpenStream(input);
        await Assert.That(stream.SizeInBytes).IsEqualTo((nuint)input.Length);
    }

    [Test]
    public async Task OpenStream_CompleteInput_ReportsNoTruncation()
    {
        using var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n{\"id\":2}\n"));
        while (stream.MoveNext())
        {
        }

        await Assert.That(stream.TruncatedBytes).IsEqualTo((nuint)0);
    }

    [Test]
    public async Task OpenStream_TruncatedTail_IsReported()
    {
        // The last document is cut off, so its bytes are left unparsed.
        using var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n{\"id\":2"));
        while (stream.MoveNext())
        {
        }

        await Assert.That(stream.TruncatedBytes).IsGreaterThan((nuint)0);
    }

    [Test]
    public async Task OpenStream_CurrentIsNullBeforeFirstMove()
    {
        using var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n"));
        await Assert.That(stream.Current).IsNull();
    }

    [Test]
    public async Task OpenStream_CurrentIsNullAfterExhaustion()
    {
        using var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n"));
        while (stream.MoveNext())
        {
        }

        await Assert.That(stream.Current).IsNull();
    }

    [Test]
    public async Task OpenStream_PreviousDocumentIsInvalidatedOnAdvance()
    {
        // The document belongs to the stream and dies when the stream moves on.
        using var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n{\"id\":2}\n"));
        stream.MoveNext();
        var first = stream.Current!;
        stream.MoveNext();
        await Assert.That(() => first.GetField("id")).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task OpenStream_AfterDispose_MoveNextThrows()
    {
        var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n"));
        stream.Dispose();
        await Assert.That(() => stream.MoveNext()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task OpenStream_DoubleDispose_IsSafe()
    {
        var stream = NdjsonParser.OpenStream(Utf8("{\"id\":1}\n"));
        stream.Dispose();
        await Assert.That(() => stream.Dispose()).ThrowsNothing();
    }

    [Test]
    public async Task OpenStream_MalformedDocument_DoesNotPoisonTheStream()
    {
        // On-Demand is lazy, so the stream hands back the bad document without complaint and
        // the failure surfaces when a field is read. A malformed document can also break into
        // more than one bad document, so this asserts the property that matters rather than a
        // fixed document count: the good documents on either side are still reachable.
        using var stream = NdjsonParser.OpenStream(
            Utf8("{\"id\":1}\n{not json}\n{\"id\":3}\n"));

        var good = new List<long>();
        int failures = 0;
        while (stream.MoveNext())
        {
            try
            {
                good.Add(SelectId(stream.Current!));
            }
            catch (SimdJsonException)
            {
                failures++;
            }
        }

        await Assert.That(good).IsEquivalentTo(new long[] { 1, 3 });
        await Assert.That(failures).IsGreaterThan(0);
    }

    [Test]
    public async Task OpenStream_NestedDocuments_ReadFully()
    {
        const string json = """
            {"id":1,"tags":["a","b"],"meta":{"ok":true}}
            {"id":2,"tags":[],"meta":{"ok":false}}
            """;
        using var stream = NdjsonParser.OpenStream(Utf8(json));

        int count = 0;
        while (stream.MoveNext())
        {
            var doc = stream.Current!;
            using var id = doc.GetField("id");
            _ = id.GetInt64();
            using var tagsVal = doc.GetField("tags");
            using var tags = tagsVal.GetArray();
            foreach (var t in tags)
            {
                t.Dispose();
            }

            using var metaVal = doc.GetField("meta");
            using var meta = metaVal.GetObject();
            using var ok = meta.GetField("ok");
            _ = ok.GetBool();
            count++;
        }

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_LargeInput_ReadsEveryDocument()
    {
        const int documents = 5_000;
        var sb = new StringBuilder();
        for (int i = 0; i < documents; i++)
        {
            sb.Append("{\"id\":").Append(i).Append(",\"name\":\"item-").Append(i).Append("\"}\n");
        }

        var ids = NdjsonParser.Parse(Utf8(sb.ToString()), SelectId);
        await Assert.That(ids.Count).IsEqualTo(documents);
        await Assert.That(ids[^1]).IsEqualTo(documents - 1L);
    }
}
