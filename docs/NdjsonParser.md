# NdjsonParser

High-performance NDJSON (Newline-Delimited JSON) parser built on top of `SimdJsonParser`.

## Overview

NDJSON (also known as JSON Lines) is a format where each line of a text stream is an independent JSON document. `NdjsonParser` provides two families of methods.

**Reading from a `Stream`**, splitting lines incrementally and parsing one document at a time:

| Method | Description |
|--------|-------------|
| `ParseAsync` | Sequential — results arrive in file order |
| `ParseParallelAsync` | Parallel — results arrive in completion order; uses all CPU cores |
| `ForEachAsync` | Parallel side-effect — no result projection |

**Reading from memory**, using simdjson's own batching parser:

| Method | Description |
|--------|-------------|
| `Parse<T>(ReadOnlySpan<byte>, Func<JsonDocument, T>, NdjsonParserOptions?)` | Projects every document, returning results in order |
| `OpenStream(ReadOnlySpan<byte>, NdjsonParserOptions?)` | Returns a [`JsonDocumentStream`](#jsondocumentstream) for manual iteration |

### Which family to use

The batching parser indexes whole batches of input at a time rather than one document per call, and where the native library was built with threads it overlaps the next batch's indexing with the current batch's parsing. That is the faster path.

**It requires the entire input to be in memory.** `BatchSize` bounds the index, not the input. For files larger than memory, or for a network stream you want to process as it arrives, the `Stream` overloads remain the right choice: they read incrementally with bounded memory.

```csharp
// In memory: use the batching parser
byte[] ndjson = File.ReadAllBytes("events.ndjson");
List<long> ids = NdjsonParser.Parse(ndjson, doc =>
{
    using var v = doc.GetField("id");
    return v.GetInt64();
});

// Too large to hold, or arriving over the network: stream it
await foreach (var id in NdjsonParser.ParseAsync(httpStream, selector)) { }
```

### Reading root scalars

A subtlety worth knowing if your NDJSON is bare scalars rather than objects, one number or string per line. simdjson normally rejects content after a root scalar, which inside a stream would mean rejecting every document but the last. The bridge reads stream documents through simdjson's `document_reference`, which disables that check, so scalar documents work in both families.

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
    public int  BatchSize              { get; init; } // default: 0 = simdjson default (1 MB)
    public bool AllowCommaSeparated    { get; init; } // default: false
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
| `BatchSize` | Bytes indexed at a time by `Parse`/`OpenStream`. `0` selects simdjson's default of 1 MB. Must exceed the largest single document, or that document cannot be parsed. Ignored by the `Stream` overloads. |
| `AllowCommaSeparated` | When `true`, `Parse`/`OpenStream` also accept commas between documents, which lets a top-level JSON array be read as a stream of its elements. Ignored by the `Stream` overloads. |

## `JsonDocumentStream`

Returned by `OpenStream` for manual iteration, when you want per-document metadata that `Parse` does not expose.

| Member | Description |
|--------|-------------|
| `MoveNext()` | Advances to the next document; `false` at the end |
| `Current` | The current document, or `null` before the first move and after the end |
| `CurrentIndex` | Byte offset of the current document within the input |
| `CurrentSource` | Raw JSON text of the current document, as a `ReadOnlySpan<byte>` |
| `SizeInBytes` | Total input size |
| `TruncatedBytes` | Bytes left unparsed at the end, usually an incomplete final document |

```csharp
using var stream = NdjsonParser.OpenStream(ndjson);
while (stream.MoveNext())
{
    using var id = stream.Current!.GetField("id");
    Console.WriteLine($"offset {stream.CurrentIndex}: {id.GetInt64()}");
}

if (stream.TruncatedBytes > 0)
{
    Console.WriteLine($"{stream.TruncatedBytes} bytes of incomplete trailing document");
}
```

> `Current` **borrows** the stream's document. It is invalidated by the next `MoveNext()` and must not be stored. Disposing the stream disposes it too.

> `TruncatedBytes` is only meaningful once you have read to the end with no document reporting an error. simdjson documents the value as arbitrary otherwise: it can exceed `SizeInBytes` or wrap around. To detect a truncated tail in other situations, track `CurrentIndex` of the last document you read successfully.

### Errors in a stream

simdjson On-Demand reports most malformed input lazily, when a field is read rather than when the document is produced. A bad document does not end the stream, so iteration can continue past it, which is how `SkipMalformedLines` is implemented for this family.

One caveat: a single malformed line can break into more than one unparseable document, so do not assume a one-to-one mapping between bad lines and errors.

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
