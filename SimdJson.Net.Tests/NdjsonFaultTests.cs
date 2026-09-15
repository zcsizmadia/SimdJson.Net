namespace SimdJson.Tests;

// ─── NDJSON error propagation and early-exit cleanup ──────────────────────────

public class NdjsonFaultTests
{
    private sealed class MarkerException : Exception;

    private static MemoryStream ToStream(string ndjson) =>
        new(System.Text.Encoding.UTF8.GetBytes(ndjson));

    private static string Lines(int count, int badLine = -1)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= count; i++)
            sb.AppendLine(i == badLine ? "{not json" : $$"""{"id":{{i}}}""");
        return sb.ToString();
    }

    private static long SelectId(JsonDocument doc)
    {
        using var v = doc.GetField("id");
        return v.GetInt64();
    }

    [Test]
    public async Task ParseParallelAsync_MalformedLine_SurfacesSimdJsonException()
    {
        using var stream = ToStream(Lines(50, badLine: 10));
        await Assert.That(async () =>
        {
            await foreach (var _ in NdjsonParser.ParseParallelAsync(stream, SelectId,
                new NdjsonParserOptions { LeaveOpen = true, SkipMalformedLines = false }))
            {
            }
        }).Throws<SimdJsonException>();
    }

    [Test]
    public async Task ParseParallelAsync_SelectorThrows_SurfacesSelectorException()
    {
        using var stream = ToStream(Lines(50));
        await Assert.That(async () =>
        {
            await foreach (var _ in NdjsonParser.ParseParallelAsync<long>(stream,
                _ => throw new MarkerException(),
                new NdjsonParserOptions { LeaveOpen = true }))
            {
            }
        }).Throws<MarkerException>();
    }

    [Test]
    public async Task ParseParallelAsync_ConsumerBreaksEarly_DisposesStream()
    {
        var stream = ToStream(Lines(500));
        await foreach (var id in NdjsonParser.ParseParallelAsync(stream, SelectId,
            new NdjsonParserOptions { MaxDegreeOfParallelism = 2, ChannelCapacity = 2 }))
        {
            _ = id;
            break;
        }

        // The reader task's finally block disposes the stream; if the workers had been
        // left blocked it would never run and CanRead would still be true.
        await Assert.That(stream.CanRead).IsFalse();
    }

    [Test]
    public async Task ForEachAsync_ActionThrows_SurfacesExceptionInsteadOfHanging()
    {
        using var stream = ToStream(Lines(500));
        var task = NdjsonParser.ForEachAsync(stream,
            _ => throw new MarkerException(),
            new NdjsonParserOptions { LeaveOpen = true, MaxDegreeOfParallelism = 1, ChannelCapacity = 2 });

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));
        await Assert.That(ReferenceEquals(completed, task)).IsTrue();
        await Assert.That(async () => await task).Throws<MarkerException>();
    }

    [Test]
    public async Task ForEachAsync_MalformedLine_SurfacesSimdJsonException()
    {
        using var stream = ToStream(Lines(200, badLine: 20));
        await Assert.That(async () => await NdjsonParser.ForEachAsync(stream,
            doc => { using var v = doc.GetField("id"); _ = v.GetInt64(); },
            new NdjsonParserOptions { LeaveOpen = true, SkipMalformedLines = false }))
            .Throws<SimdJsonException>();
    }

    [Test]
    public async Task ParseAsync_BomSplitAcrossReads_FirstLineStillParsed()
    {
        var payload = new List<byte> { 0xEF, 0xBB, 0xBF };
        payload.AddRange(System.Text.Encoding.UTF8.GetBytes("{\"id\":1}\n{\"id\":2}\n"));

        using var stream = new DripStream(payload.ToArray(), bytesPerRead: 1);
        var ids = new List<long>();
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
            ids.Add(id);

        await Assert.That(ids).IsEquivalentTo(new long[] { 1, 2 });
    }

    /// <summary>A stream that returns at most <c>bytesPerRead</c> bytes per read, like a socket.</summary>
    private sealed class DripStream(byte[] data, int bytesPerRead) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int n = Math.Min(Math.Min(bytesPerRead, buffer.Length), data.Length - _position);
            if (n <= 0)
            {
                return 0;
            }

            data.AsSpan(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
