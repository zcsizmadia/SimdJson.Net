# NdjsonParser

High-performance NDJSON (Newline-Delimited JSON) parser built on top of `SimdJsonParser`.

## Overview

NDJSON (also known as JSON Lines) is a format where each line of a text stream is an independent JSON document. `NdjsonParser` provides three modes:

| Method | Description |
|--------|-------------|
| `ParseAsync` | Sequential — results arrive in file order |
| `ParseParallelAsync` | Parallel — results arrive in completion order; uses all CPU cores |
| `ForEachAsync` | Parallel side-effect — no result projection |

## NdjsonParserOptions

```csharp
public sealed class NdjsonParserOptions
{
    public static readonly NdjsonParserOptions Default = new();

    public int  MaxDegreeOfParallelism { get; init; } // default: Environment.ProcessorCount
    public int  ChannelCapacity        { get; init; } // default: 0 = auto (DOP×4)
    public int  ReadBufferSize         { get; init; } // default: 65_536 (64 KiB)
    public int  InitialLineBufferSize  { get; init; } // default: 4_096  (4 KiB)
    public bool SkipMalformedLines     { get; init; } // default: true
    public bool SkipEmptyLines         { get; init; } // default: true
    public bool LeaveOpen              { get; init; } // default: false
}
```

| Property | Purpose |
|----------|---------|
| `MaxDegreeOfParallelism` | Worker task count for `ParseParallelAsync`/`ForEachAsync`. Scale up for CPU-bound projections; keep at 1 for I/O-bound sources. |
| `ChannelCapacity` | Backpressure channel size. `0` auto-selects `DOP×4`. Larger = higher peak throughput at the cost of more buffered lines. |
| `ReadBufferSize` | Internal stream read buffer. Larger values reduce `ReadAsync` syscall frequency; smaller values reduce latency to first result. |
| `InitialLineBufferSize` | `ArrayPool<byte>` rent hint per line. Set to ~average line length to minimise pool bucket misses. |
| `SkipMalformedLines` | When `true` (default), bad JSON lines are silently ignored. When `false`, the first error propagates and cancels all workers. |
| `SkipEmptyLines` | When `true` (default), blank lines are skipped without error. |
| `LeaveOpen` | When `true`, the input stream is not disposed when parsing ends. |

## ParseAsync

Sequential parser that yields results in file order.

```csharp
public static async IAsyncEnumerable<T> ParseAsync<T>(
    Stream stream,
    Func<JsonDocument, T> selector,
    NdjsonParserOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
```

The `selector` is invoked on the calling thread for each line. The `JsonDocument` passed to it is valid **only for the duration of the call**; do not capture it.

```csharp
await foreach (var name in NdjsonParser.ParseAsync(stream, doc =>
{
    using var v = doc.GetField("name");
    return v.GetString();
}))
    Console.WriteLine(name);
```

## ParseParallelAsync

Parallel parser — multiple worker tasks each own a private `SimdJsonParser`. Results arrive in **completion order** (not file order).

```csharp
public static async IAsyncEnumerable<T> ParseParallelAsync<T>(
    Stream stream,
    Func<JsonDocument, T> selector,
    NdjsonParserOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
```

Architecture:
1. A single reader task scans the stream using vectorized `Span<byte>.IndexOf` (SIMD on .NET 8+) and copies each line into an `ArrayPool<byte>` buffer.
2. Lines flow through a bounded `Channel` to `MaxDegreeOfParallelism` worker tasks.
3. Each worker parses its line and pushes the projected result into a second bounded channel back to the caller.
4. The caller consumes results via `await foreach`.

```csharp
long sum = 0;
await foreach (var score in NdjsonParser.ParseParallelAsync(stream, doc =>
{
    using var v = doc.GetField("score");
    return v.GetDouble();
}, new NdjsonParserOptions { MaxDegreeOfParallelism = 8 }))
    sum += (long)score;
```

## ForEachAsync

Parallel side-effect iteration. Use when no result needs to be collected.

```csharp
public static async Task ForEachAsync(
    Stream stream,
    Action<JsonDocument> action,
    NdjsonParserOptions? options = null,
    CancellationToken cancellationToken = default)
```

The `action` is called concurrently from worker tasks. Use `Interlocked` or other thread-safe constructs when writing to shared state.

```csharp
int count = 0;
await NdjsonParser.ForEachAsync(stream, doc =>
{
    using var v = doc.GetField("id");
    _ = v.GetInt64();
    Interlocked.Increment(ref count);
});
Console.WriteLine($"Processed {count} records");
```

## Edge case handling

| Situation | Default behaviour |
|-----------|------------------|
| Empty lines | Skipped (`SkipEmptyLines = true`) |
| CRLF line endings | Handled automatically — `\r` is stripped before parsing |
| UTF-8 BOM at start of stream | Stripped automatically |
| No trailing newline | Last line is parsed correctly |
| Malformed JSON line | Skipped (`SkipMalformedLines = true`); set to `false` to throw |
| Cancellation | All workers receive the token and exit promptly |

## Performance notes

- **Memory**: each line is copied into an `ArrayPool<byte>` buffer and returned after parsing — zero heap allocation per line in steady state.
- **SIMD newline scan**: `Span<byte>.IndexOf((byte)'\n')` is auto-vectorized on .NET 8+ (AVX2/SSE4.2), scanning up to 32 bytes per cycle.
- **No shared parser state**: each worker owns its own `SimdJsonParser` instance — no locking, no cache line contention.
- **Backpressure**: bounded channels prevent the reader from outrunning the workers, keeping memory usage predictable even for multi-gigabyte files.

## Error handling

When `SkipMalformedLines = false`, any `SimdJsonException` thrown during parsing propagates from the iterator:

```csharp
try
{
    await foreach (var r in NdjsonParser.ParseAsync(stream, selector,
        new NdjsonParserOptions { SkipMalformedLines = false }))
    { }
}
catch (SimdJsonException ex)
{
    Console.WriteLine($"Parse error on malformed line: {ex.Message}");
}
```

For `ParseParallelAsync`, the exception from the first faulting worker is re-thrown after all workers complete (via `ExceptionDispatchInfo` to preserve the original stack trace).
