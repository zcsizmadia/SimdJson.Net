using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SimdJson;

/// <summary>
/// Options controlling NDJSON parsing behaviour.
/// </summary>
public sealed class NdjsonParserOptions
{
    /// <summary>Default options instance (all defaults applied).</summary>
    public static readonly NdjsonParserOptions Default = new();

    /// <summary>
    /// Number of concurrent worker tasks for parallel parsing.
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Capacity of the work channels between the reader and workers, and between workers
    /// and the consumer. <c>0</c> (default) auto-selects <c>MaxDegreeOfParallelism × 4</c>.
    /// Increase for bursty workloads; decrease to reduce peak memory.
    /// </summary>
    public int ChannelCapacity { get; init; } = 0;

    /// <summary>
    /// Size of the internal stream read buffer in bytes. Defaults to 65,536 (64 KiB).
    /// Larger values reduce <c>ReadAsync</c> syscall frequency; smaller values reduce
    /// latency to the first result.
    /// </summary>
    public int ReadBufferSize { get; init; } = 65_536;

    /// <summary>
    /// <see cref="ArrayPool{T}"/> rent hint for individual line buffers (bytes).
    /// Set to the expected average NDJSON line length to minimise pool bucket misses.
    /// Defaults to 4,096 (4 KiB).
    /// </summary>
    public int InitialLineBufferSize { get; init; } = 4_096;

    /// <summary>
    /// When <see langword="true"/> (default), lines that fail to parse are silently skipped.
    /// When <see langword="false"/>, a parse error propagates and cancels all workers.
    /// </summary>
    public bool SkipMalformedLines { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/> (default), blank lines are silently ignored.
    /// </summary>
    public bool SkipEmptyLines { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, the input stream is left open after parsing completes.
    /// Defaults to <see langword="false"/> (stream is disposed when parsing ends).
    /// </summary>
    public bool LeaveOpen { get; init; } = false;
}

/// <summary>
/// High-performance parallel NDJSON (Newline-Delimited JSON) parser built on
/// <see cref="SimdJsonParser"/> and <see cref="System.Threading.Channels"/>.
/// </summary>
/// <remarks>
/// <para><b>Architecture (parallel path):</b></para>
/// <list type="bullet">
///   <item>A dedicated reader task scans the stream with vectorized
///         <see cref="MemoryExtensions.IndexOf{T}(Span{T},T)"/> (SIMD on .NET 8+) and
///         copies each line into an <see cref="ArrayPool{T}"/> buffer with zero heap
///         allocation per line.</item>
///   <item>A bounded <see cref="Channel{T}"/> provides automatic backpressure so the
///         reader pauses when workers are busy, keeping memory usage predictable.</item>
///   <item><see cref="NdjsonParserOptions.MaxDegreeOfParallelism"/> worker tasks each own
///         a private <see cref="SimdJsonParser"/> — no locking, no shared parser state.
///         Each parser's SIMD buffer stays hot in the owning core's cache.</item>
///   <item>Parsed results flow through a second bounded channel back to the caller as
///         <see cref="IAsyncEnumerable{T}"/>.</item>
/// </list>
/// <para><b>.NET version acceleration (passive — no code changes required):</b></para>
/// <list type="bullet">
///   <item><b>net8+</b>: <c>Span&lt;byte&gt;.IndexOf</c> uses AVX2/SSE4.2 vectorisation,
///         scanning 32 bytes per cycle for newlines.</item>
///   <item><b>net9+</b>: improved <see cref="Channel{T}"/> lock-free fast paths reduce
///         producer/consumer handoff overhead at high message rates.</item>
///   <item><b>net10+</b>: faster async state machine codegen; improved
///         <see cref="ArrayPool{T}"/> thread-local allocation buffers (TLABs) reduce
///         contention on the pool at high parallelism.</item>
/// </list>
/// </remarks>
public static class NdjsonParser
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses an NDJSON stream sequentially on the calling thread, projecting each line
    /// through <paramref name="selector"/>. Results are yielded in document order.
    /// </summary>
    /// <typeparam name="T">The result type produced from each JSON document.</typeparam>
    /// <param name="stream">UTF-8 encoded NDJSON stream.</param>
    /// <param name="selector">
    /// Called once per parsed document. The <see cref="JsonDocument"/> is disposed
    /// immediately after the delegate returns — do not store it.
    /// </param>
    /// <param name="options">Parser options; <see langword="null"/> uses <see cref="NdjsonParserOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<T> ParseAsync<T>(
        Stream stream,
        Func<JsonDocument, T> selector,
        NdjsonParserOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(selector);
        options ??= NdjsonParserOptions.Default;

        var parser = new SimdJsonParser();
        try
        {
            // yield return inside try-finally (no catch) is allowed by the C# spec.
            await foreach (var (buf, len) in ReadLinesAsync(stream, options, cancellationToken).ConfigureAwait(false))
            {
                // try-catch lives in the helper — keeps yield out of try-catch (CS1626).
                if (TryParseAndSelect(buf, len, parser, selector, options, out T result))
                    yield return result;
            }
        }
        finally
        {
            parser.Dispose();
            if (!options.LeaveOpen)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Parses an NDJSON stream in parallel across
    /// <see cref="NdjsonParserOptions.MaxDegreeOfParallelism"/> worker tasks, each owning
    /// a private <see cref="SimdJsonParser"/>. Throughput scales linearly with core count.
    /// <b>Result order is not guaranteed.</b>
    /// </summary>
    /// <typeparam name="T">The result type produced from each JSON document.</typeparam>
    /// <param name="stream">UTF-8 encoded NDJSON stream.</param>
    /// <param name="selector">
    /// Called concurrently from multiple tasks — must be thread-safe with respect to any
    /// shared state it captures. The <see cref="JsonDocument"/> is disposed after the
    /// delegate returns — do not store it.
    /// </param>
    /// <param name="options">Parser options; <see langword="null"/> uses <see cref="NdjsonParserOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<T> ParseParallelAsync<T>(
        Stream stream,
        Func<JsonDocument, T> selector,
        NdjsonParserOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(selector);
        options ??= NdjsonParserOptions.Default;

        int dop = Math.Max(1, options.MaxDegreeOfParallelism);
        int cap = options.ChannelCapacity > 0 ? options.ChannelCapacity : dop * 4;

        // Lines channel: reader → workers
        var lineChannel = Channel.CreateBounded<(byte[] buf, int len)>(
            new BoundedChannelOptions(cap)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
            });

        // Results channel: workers → consumer
        var resultChannel = Channel.CreateBounded<T>(
            new BoundedChannelOptions(cap)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true,
            });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Stores the first worker exception (workers capture their own faults).
        Exception? workerFault = null;

        // ── Reader task ──────────────────────────────────────────────────────
        var readerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in ReadLinesAsync(stream, options, cts.Token).ConfigureAwait(false))
                    await lineChannel.Writer.WriteAsync(line, cts.Token).ConfigureAwait(false);
                lineChannel.Writer.Complete();
            }
            catch (Exception ex)
            {
                lineChannel.Writer.Complete(ex);
            }
            finally
            {
                if (!options.LeaveOpen)
                    await stream.DisposeAsync().ConfigureAwait(false);
            }
        }, cts.Token);

        // ── Worker tasks (one SimdJsonParser per task — no locking) ─────────
        var workerTasks = new Task[dop];
        for (int i = 0; i < dop; i++)
        {
            workerTasks[i] = Task.Run(async () =>
            {
                var parser = new SimdJsonParser();
                try
                {
                    await foreach (var (buf, len) in lineChannel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
                    {
                        if (TryParseAndSelect(buf, len, parser, selector, options, out T result))
                            await resultChannel.Writer.WriteAsync(result, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Capture first fault, then cancel everything.
                    Interlocked.CompareExchange(ref workerFault, ex, null);
                    cts.Cancel();
                }
                finally
                {
                    parser.Dispose();
                }
            }, cts.Token);
        }

        // Close the result channel once all workers are done.
        _ = Task.WhenAll(workerTasks)
                .ContinueWith(_ => resultChannel.Writer.TryComplete(), TaskScheduler.Default);

        // ── Stream results to the caller ─────────────────────────────────────
        // yield return is NOT inside a try-catch here (only inside the try-finally from
        // 'using var cts' above), so CS1626 does not apply.
        await foreach (var item in resultChannel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
            yield return item;

        // Await background tasks (exceptions already captured in workerFault).
        try { await readerTask.ConfigureAwait(false); } catch { }
        try { await Task.WhenAll(workerTasks).ConfigureAwait(false); } catch { }

        if (workerFault is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(workerFault).Throw();
    }

    /// <summary>
    /// Processes each line of an NDJSON stream in parallel without returning results.
    /// Returns after every line has been processed.
    /// </summary>
    public static async Task ForEachAsync(
        Stream stream,
        Action<JsonDocument> action,
        NdjsonParserOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(action);
        options ??= NdjsonParserOptions.Default;

        int dop = Math.Max(1, options.MaxDegreeOfParallelism);
        int cap = options.ChannelCapacity > 0 ? options.ChannelCapacity : dop * 4;

        var lineChannel = Channel.CreateBounded<(byte[] buf, int len)>(
            new BoundedChannelOptions(cap)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
            });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var readerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in ReadLinesAsync(stream, options, cts.Token).ConfigureAwait(false))
                    await lineChannel.Writer.WriteAsync(line, cts.Token).ConfigureAwait(false);
                lineChannel.Writer.Complete();
            }
            catch (Exception ex)
            {
                lineChannel.Writer.Complete(ex);
            }
            finally
            {
                if (!options.LeaveOpen)
                    await stream.DisposeAsync().ConfigureAwait(false);
            }
        }, cts.Token);

        var workerTasks = new Task[dop];
        for (int i = 0; i < dop; i++)
        {
            workerTasks[i] = Task.Run(async () =>
            {
                var parser = new SimdJsonParser();
                try
                {
                    await foreach (var (buf, len) in lineChannel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
                        TryParseAction(buf, len, parser, action, options);
                }
                finally
                {
                    parser.Dispose();
                }
            }, cts.Token);
        }

        await readerTask.ConfigureAwait(false);
        await Task.WhenAll(workerTasks).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses one line buffer and calls <paramref name="selector"/>. Returns the buffer to
    /// the pool in its finally block. Extracted from the iterator methods so that
    /// try-catch does not appear around yield return (C# CS1626 restriction).
    /// </summary>
    private static bool TryParseAndSelect<T>(
        byte[] buf, int len, SimdJsonParser parser,
        Func<JsonDocument, T> selector, NdjsonParserOptions options, out T result)
    {
        try
        {
            using var doc = parser.Parse(buf.AsSpan(0, len));
            result = selector(doc);
            return true;
        }
        catch (SimdJsonException) when (options.SkipMalformedLines)
        {
            result = default!;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static void TryParseAction(
        byte[] buf, int len, SimdJsonParser parser,
        Action<JsonDocument> action, NdjsonParserOptions options)
    {
        try
        {
            using var doc = parser.Parse(buf.AsSpan(0, len));
            action(doc);
        }
        catch (SimdJsonException) when (options.SkipMalformedLines) { }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    /// <summary>
    /// Reads <paramref name="stream"/> and yields one rented <see cref="ArrayPool{T}"/>
    /// buffer per line. The caller must return each buffer to the pool after use.
    /// </summary>
    /// <remarks>
    /// Hot-path notes:
    /// <list type="bullet">
    ///   <item><c>Span&lt;byte&gt;.IndexOf((byte)'\n')</c> is AVX2/SSE4.2-vectorised on
    ///         .NET 8+, scanning 32 bytes per cycle — no extra package required.</item>
    ///   <item>The read buffer is rented once and reused across all iterations. It
    ///         auto-grows (doubling) when a line is longer than the current buffer.</item>
    ///   <item>UTF-8 BOM (EF BB BF) is stripped from the start of the stream once.</item>
    ///   <item>Both LF and CRLF line endings are handled.</item>
    /// </list>
    /// </remarks>
    private static async IAsyncEnumerable<(byte[] buf, int len)> ReadLinesAsync(
        Stream stream,
        NdjsonParserOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int readBufSize = Math.Max(options.ReadBufferSize, 4096);
        byte[] readBuf = ArrayPool<byte>.Shared.Rent(readBufSize);
        int offset = 0;
        bool firstChunk = true;

        // yield return inside try-finally (no catch) is permitted by the C# spec.
        try
        {
            while (true)
            {
                // Auto-grow when a single line fills the entire read buffer.
                if (offset >= readBuf.Length)
                {
                    byte[] larger = ArrayPool<byte>.Shared.Rent(readBuf.Length * 2);
                    readBuf.AsSpan(0, offset).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(readBuf);
                    readBuf = larger;
                }

                int read = await stream.ReadAsync(
                    readBuf.AsMemory(offset, readBuf.Length - offset),
                    cancellationToken).ConfigureAwait(false);

                int total = offset + read;
                bool eof = read == 0;

                int lineStart = 0;

                // Strip UTF-8 BOM from the very beginning of the stream (one-time).
                if (firstChunk)
                {
                    firstChunk = false;
                    if (total >= 3 &&
                        readBuf[0] == 0xEF && readBuf[1] == 0xBB && readBuf[2] == 0xBF)
                        lineStart = 3;
                }

                // Vectorized newline scan — IndexOf uses AVX2/SSE4.2 on .NET 8+.
                while (true)
                {
                    int searchLen = total - lineStart;
                    if (searchLen <= 0) break;

                    int nlOffset = readBuf.AsSpan(lineStart, searchLen).IndexOf((byte)'\n');

                    if (nlOffset < 0)
                    {
                        // No newline in remaining data — emit final line only at EOF.
                        if (eof && searchLen > 0)
                        {
                            int lineLen = searchLen;
                            if (lineLen > 0 && readBuf[lineStart + lineLen - 1] == '\r')
                                lineLen--;
                            if (lineLen > 0 || !options.SkipEmptyLines)
                                yield return RentLine(readBuf, lineStart, lineLen, options);
                        }
                        break;
                    }

                    int nlPos = lineStart + nlOffset;
                    int lineEnd = nlPos;

                    // Trim trailing \r (CRLF).
                    if (lineEnd > lineStart && readBuf[lineEnd - 1] == '\r')
                        lineEnd--;

                    int len = lineEnd - lineStart;
                    if (len > 0 || !options.SkipEmptyLines)
                        yield return RentLine(readBuf, lineStart, len, options);

                    lineStart = nlPos + 1; // advance past '\n'
                }

                if (eof) break;

                // Compact: shift unprocessed tail to the front.
                int leftover = total - lineStart;
                if (leftover > 0 && lineStart > 0)
                    readBuf.AsSpan(lineStart, leftover).CopyTo(readBuf);
                offset = leftover;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuf);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (byte[] buf, int len) RentLine(
        byte[] src, int start, int len, NdjsonParserOptions options)
    {
        // Rent with the user-supplied hint so pool bucket matches expected line sizes.
        int rentSize = len > 0 ? Math.Max(len, options.InitialLineBufferSize) : 1;
        byte[] buf = ArrayPool<byte>.Shared.Rent(rentSize);
        if (len > 0)
            src.AsSpan(start, len).CopyTo(buf);
        return (buf, len);
    }
}
