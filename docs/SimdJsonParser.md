# `SimdJsonParser`

The main entry point for parsing JSON. One instance per thread; not thread-safe.

## Thread-local shared instance

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice"}""");
```

`SimdJsonParser.Shared` is a thread-local instance that is never disposed. Use it when you do not need to control the parser lifetime manually.

> **One document at a time**: a single `SimdJsonParser` can only have one live `JsonDocument`. Dispose the document before calling `Parse` again on the same instance. See [Design Notes](DesignNotes.md) for details.

## Constructors

| Signature | Description |
|-----------|-------------|
| `new SimdJsonParser()` | Create a new parser with the default capacity |
| `new SimdJsonParser(nuint maxCapacity)` | Create a parser with a custom document size cap |

## Parsing

| Member | Description |
|--------|-------------|
| `Parse(ReadOnlySpan<byte>)` | Parse UTF-8 bytes directly (zero-copy) |
| `Parse(string)` | Parse a .NET string (UTF-8 transcoding via stack or `ArrayPool`) |
| `ParseAllowIncompleteJson(ReadOnlySpan<byte>)` | Parse a potentially truncated UTF-8 document (experimental) |
| `ParseAllowIncompleteJson(string)` | Parse a potentially truncated string (experimental) |
| `ParseAsync(string, CancellationToken)` | Parse a string on the thread pool |
| `ParseAsync(Stream, CancellationToken)` | Read a stream then parse |

All `Parse` methods return a `JsonDocument` that **must be disposed**.

## Properties

| Member | Description |
|--------|-------------|
| `Capacity` | Current internal buffer size in bytes; `0` before the first parse |
| `MaxCapacity` *(get/set)* | Maximum allowed document size in bytes |
| `MaxDepth` | Maximum JSON nesting depth the parser supports |

## Static utilities

| Member | Description |
|--------|-------------|
| `GetVersion()` | Returns the simdjson version string (e.g. `"4.6.3"`) |
| `Minify(string)` | Remove all insignificant whitespace; returns a `string` |
| `MinifyUtf8(ReadOnlySpan<byte>)` | Minify UTF-8 JSON bytes; returns `byte[]` |
| `ValidateUtf8(ReadOnlySpan<byte>)` | Returns `true` if the bytes are valid UTF-8 |
| `ValidateUtf8(string)` | UTF-8 validation for a .NET string |

## Examples

```csharp
// Thread-local shared parser
using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");

// Dedicated parser with capacity limit (10 MB)
using var parser = new SimdJsonParser(10 * 1024 * 1024);
using var doc2 = parser.Parse(jsonBytes);

// Async from stream
await using var file = File.OpenRead("data.json");
using var doc3 = await parser.ParseAsync(file, cancellationToken);

// Utilities
string mini = SimdJsonParser.Minify("""{ "a" : 1 , "b" : 2 }"""); // {"a":1,"b":2}
bool ok      = SimdJsonParser.ValidateUtf8(someBytes);
string ver   = SimdJsonParser.GetVersion();  // "4.6.3"
```

---

← [API Reference](API.md)
