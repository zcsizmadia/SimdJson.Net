# SimdJson.Net

A high-performance .NET wrapper for [simdjson](https://github.com/simdjson/simdjson) v4.6.3, exposing the On-Demand API via a thin C ABI bridge.

- **`SimdJson.Net`** — idiomatic C# API: `SimdJsonParser`, `JsonDocument`, `JsonValue`, `JsonArray`, `JsonObject`

All native binaries are compiled from source via GitHub Actions — transparent, reproducible, and auditable.

## Quick Start

```csharp
// Thread-local shared parser — no allocation per call
using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice","age":30}""");
using var name = doc.GetField("name");
Console.WriteLine(name.GetString()); // Alice

// Parse from UTF-8 span (zero-copy)
ReadOnlySpan<byte> utf8 = """{"x":1}"""u8;
using var doc2 = SimdJsonParser.Shared.Parse(utf8);

// Async parse from stream
using var doc3 = await parser.ParseAsync(stream, cancellationToken);

// Iterate an array
using var arr = doc.GetField("items").GetArray();
foreach (var item in arr)
{
    Console.WriteLine(item.GetString());
}

// Iterate an object
using var obj = doc.GetObject();
foreach (var prop in obj)
{
    Console.WriteLine($"{prop.Name} = {prop.Value.GetInt64()}");
    prop.Value.Dispose();
}

// JSON Pointer
using var city = doc.AtPointer("/address/city");

// Chain indexers
using var val = doc["address"]["city"];

// Get simdjson version
Console.WriteLine(SimdJsonParser.GetVersion()); // e.g. "4.6.3"
```

## Installation

```bash
dotnet add package SimdJson.Net
```

## API Reference

### `SimdJsonParser`

| Member | Description |
|--------|-------------|
| `SimdJsonParser.Shared` | Thread-local instance — do not dispose |
| `new SimdJsonParser()` | Create a new parser (one per thread) |
| `Parse(ReadOnlySpan<byte>)` | Parse UTF-8 bytes directly (zero-copy) |
| `Parse(string)` | Parse a .NET string (UTF-8 transcoding on stack/pool) |
| `ParseAsync(string, CancellationToken)` | Parse a string on the thread pool |
| `ParseAsync(Stream, CancellationToken)` | Read stream then parse |
| `GetVersion()` | Returns simdjson version string (e.g. `"4.6.3"`) |

### `JsonDocument` *(IDisposable)*

| Member | Description |
|--------|-------------|
| `ValueKind` | `JsonValueKind` of the document root |
| `GetArray()` | Root as `JsonArray` |
| `GetObject()` | Root as `JsonObject` |
| `GetField(string)` | Field by name (root must be object) |
| `AtPointer(string)` | RFC 6901 JSON Pointer lookup |
| `this[string]` | Indexer — same as `GetField` |

### `JsonValue` *(IDisposable)*

| Member | Description |
|--------|-------------|
| `ValueKind` | `JsonValueKind` of this value |
| `GetString()` | As `string` |
| `GetStringSpan()` | As `ReadOnlySpan<byte>` (zero allocation) |
| `GetDouble()` | As `double` |
| `GetInt64()` | As `long` |
| `GetUInt64()` | As `ulong` |
| `GetBool()` | As `bool` |
| `IsNull()` | Returns `true` if JSON null |
| `GetArray()` | As `JsonArray` |
| `GetObject()` | As `JsonObject` |
| `GetField(string)` / `this[string]` | Field by name |

### `JsonArray` *(IDisposable, IEnumerable\<JsonValue\>)*

| Member | Description |
|--------|-------------|
| `Count` | Number of elements (full scan) |
| `foreach` | Iterate elements as `JsonValue` |

### `JsonObject` *(IDisposable, IEnumerable\<JsonProperty\>)*

| Member | Description |
|--------|-------------|
| `Count` | Number of fields (full scan) |
| `GetField(string)` / `this[string]` | Field by name |
| `foreach` | Iterate as `JsonProperty` (`Name` + `Value`) |

### `JsonValueKind`

`Array`, `Object`, `Number`, `String`, `Boolean`, `Null`, `Unknown`

### `SimdJsonException`

Thrown on any native error. `ErrorCode` holds the raw bridge error code.

## Design Notes

- **One parser per thread**: simdjson On-Demand parsers are not thread-safe. Use `SimdJsonParser.Shared` for effortless thread-local access.
- **One document at a time**: a single parser instance can only have one live `JsonDocument`. Dispose the document before parsing the next one.
- **Forward-only iteration**: simdjson On-Demand is a streaming parser. Accessing fields in a different order than they appear in the JSON, or iterating an already-consumed iterator, may throw `SimdJsonException`.
- **Dispose discipline**: every `JsonDocument`, `JsonValue`, `JsonArray`, and `JsonObject` holds a native handle. Always dispose them, preferably with `using`.

## Supported Runtimes

| RID | Platform |
|-----|----------|
| `win-x64` | Windows x64 |
| `win-arm64` | Windows ARM64 |
| `linux-x64` | Linux glibc x64 |
| `linux-arm64` | Linux glibc ARM64 |
| `linux-musl-x64` | Linux musl x64 |
| `linux-musl-arm64` | Linux musl ARM64 |
| `osx-x64` | macOS x64 |
| `osx-arm64` | macOS ARM64 (Apple Silicon) |

## Upstream and Attribution

The native `SimdJsonNative` shared library wraps [simdjson](https://github.com/simdjson/simdjson) by Daniel Lemire and contributors, licensed under Apache 2.0. This package is an independent .NET distribution; all simdjson rights belong to the original authors.

- Upstream: https://github.com/simdjson/simdjson
- This repository: https://github.com/zcsizmadia/SimdJson.Net

## License

MIT. See [LICENSE](LICENSE).
