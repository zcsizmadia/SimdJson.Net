using System.Collections;
using System.Text;

namespace SimdJson.Tests;

using System.Threading;

public class NdjsonParserTests
{
    private static MemoryStream ToStream(string ndjson) =>
        new(System.Text.Encoding.UTF8.GetBytes(ndjson));

    private static long SelectId(JsonDocument doc)
    {
        using var v = doc.GetField("id");
        return v.GetInt64();
    }

    // ── ParseAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task ParseAsync_ProjectsAllLines_InOrder()
    {
        using var stream = ToStream("{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n");
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task ParseAsync_SkipsEmptyLines_ByDefault()
    {
        using var stream = ToStream("{\"id\":1}\n\n{\"id\":2}\n\n");
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ParseAsync_HandlesCRLF()
    {
        using var stream = ToStream("{\"id\":10}\r\n{\"id\":20}\r\n");
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids).IsEquivalentTo(new long[] { 10, 20 });
    }

    [Test]
    public async Task ParseAsync_StripsUtf8Bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes("{\"id\":42}\n"))
            .ToArray();
        using var stream = new MemoryStream(bytes);
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids).IsEquivalentTo(new long[] { 42 });
    }

    [Test]
    public async Task ParseAsync_SkipsMalformedLines_ByDefault()
    {
        using var stream = ToStream("{\"id\":1}\nNOT_JSON\n{\"id\":2}\n");
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2 });
    }

    [Test]
    public async Task ParseAsync_ThrowsOnMalformed_WhenSkipMalformedLinesFalse()
    {
        using var stream = ToStream("{\"id\":1}\nBAD\n{\"id\":2}\n");
        await Assert.That(async () =>
        {
            await foreach (var _ in NdjsonParser.ParseAsync(stream, SelectId,
                new NdjsonParserOptions { LeaveOpen = true, SkipMalformedLines = false }))
            { }
        }).Throws<SimdJsonException>();
    }

    [Test]
    public async Task ParseAsync_HandlesNoTrailingNewline()
    {
        using var stream = ToStream("{\"id\":5}");
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids).IsEquivalentTo(new long[] { 5 });
    }

    [Test]
    public async Task ParseAsync_SmallReadBuffer_StillParsesCorrectly()
    {
        // ReadBufferSize=16 forces frequent compaction and buffer-growth paths.
        using var stream = ToStream("{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n");
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true, ReadBufferSize = 16 }))
            ids.Add(id);
        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task ParseAsync_LeaveOpen_False_DisposesStream()
    {
        var stream = ToStream("{\"id\":1}\n");
        await foreach (var _ in NdjsonParser.ParseAsync(stream, SelectId)) { }
        await Assert.That(() => stream.ReadByte()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ParseAsync_LeaveOpen_True_StreamRemainsOpen()
    {
        var stream = ToStream("{\"id\":1}\n");
        await foreach (var _ in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true })) { }
        stream.Seek(0, SeekOrigin.Begin);
        await Assert.That(stream.Length).IsGreaterThan(0L);
        await stream.DisposeAsync();
    }

    // ── ParseParallelAsync ────────────────────────────────────────────────────

    [Test]
    public async Task ParseParallelAsync_AllLinesProjected()
    {
        const int lineCount = 100;
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
            sb.AppendLine($"{{\"id\":{i}}}");

        using var stream = ToStream(sb.ToString());
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        await foreach (var id in NdjsonParser.ParseParallelAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);

        await Assert.That(ids.Count).IsEqualTo(lineCount);
        await Assert.That(ids.Sum()).IsEqualTo((long)lineCount * (lineCount + 1) / 2);
    }

    [Test]
    public async Task ParseParallelAsync_SkipsMalformedLines_ByDefault()
    {
        using var stream = ToStream("{\"id\":1}\nBAD\n{\"id\":2}\n");
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        await foreach (var id in NdjsonParser.ParseParallelAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);
        await Assert.That(ids.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ParseParallelAsync_SingleWorker_ProcessesAllLines()
    {
        using var stream = ToStream("{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n");
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        await foreach (var id in NdjsonParser.ParseParallelAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true, MaxDegreeOfParallelism = 1 }))
            ids.Add(id);
        await Assert.That(ids.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ParseParallelAsync_SmallChannelCapacity_StillProcessesAllLines()
    {
        const int lineCount = 20;
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
            sb.AppendLine($"{{\"id\":{i}}}");

        using var stream = ToStream(sb.ToString());
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        await foreach (var id in NdjsonParser.ParseParallelAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true, ChannelCapacity = 1 }))
            ids.Add(id);
        await Assert.That(ids.Count).IsEqualTo(lineCount);
    }

    // ── ForEachAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task ForEachAsync_CountsAllLines()
    {
        const int lineCount = 50;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lineCount; i++)
            sb.AppendLine($"{{\"v\":{i}}}");

        using var stream = ToStream(sb.ToString());
        int count = 0;
        await NdjsonParser.ForEachAsync(stream, doc =>
        {
            using var v = doc.GetField("v");
            _ = v.GetInt64();
            Interlocked.Increment(ref count);
        }, new NdjsonParserOptions { LeaveOpen = true });
        await Assert.That(count).IsEqualTo(lineCount);
    }

    [Test]
    public async Task ForEachAsync_Parallel_SumsCorrectly()
    {
        const int lineCount = 200;
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
            sb.AppendLine($"{{\"n\":{i}}}");

        using var stream = ToStream(sb.ToString());
        long sum = 0;
        await NdjsonParser.ForEachAsync(stream, doc =>
        {
            using var v = doc.GetField("n");
            Interlocked.Add(ref sum, v.GetInt64());
        }, new NdjsonParserOptions { LeaveOpen = true });
        await Assert.That(sum).IsEqualTo((long)lineCount * (lineCount + 1) / 2);
    }
}
