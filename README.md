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
| `Minify(string)` | Remove all insignificant whitespace from a JSON string |
| `MinifyUtf8(ReadOnlySpan<byte>)` | Minify UTF-8 JSON bytes, returns `byte[]` |
| `ValidateUtf8(ReadOnlySpan<byte>)` | Returns `true` if the bytes are valid UTF-8 |
| `ValidateUtf8(string)` | UTF-8 validation for .NET strings |

### `JsonDocument` *(IDisposable)*

| Member | Description |
|--------|-------------|
| `ValueKind` | `JsonValueKind` of the document root |
| `GetArray()` | Root as `JsonArray` |
| `GetObject()` | Root as `JsonObject` |
| `GetField(string)` / `this[string]` | Field by name (order-insensitive, root must be object) |
| `FindField(string)` | Order-sensitive forward-search field lookup |
| `AtPointer(string)` | RFC 6901 JSON Pointer lookup (e.g. `"/items/0/name"`) |
| `AtPath(string)` | JSONPath lookup (e.g. `"$.items[0].name"`) |
| `TryGetField(string, out JsonValue?)` | Non-throwing `GetField` |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |
| `Rewind()` | Reset the document iterator to the start |
| `GetRawJson()` | Full raw JSON of the document root as a string |

### `JsonValue` *(IDisposable)*

| Member | Description |
|--------|-------------|
| `ValueKind` | `JsonValueKind` of this value |
| `GetString()` | As `string` |
| `GetStringSpan()` | As `ReadOnlySpan<byte>` (zero allocation) |
| `GetDouble()` | As `double` |
| `GetInt64()` | As `long` |
| `GetUInt64()` | As `ulong` |
| `GetInt32()` | As `int` |
| `GetFloat()` | As `float` |
| `GetDecimal()` | As `decimal` |
| `GetBool()` | As `bool` |
| `IsNull()` | Returns `true` if JSON null |
| `GetArray()` | As `JsonArray` |
| `GetObject()` | As `JsonObject` |
| `GetField(string)` / `this[string]` | Field by name (order-insensitive) |
| `FindField(string)` | Order-sensitive forward-search field lookup |
| `AtPointer(string)` | JSON Pointer from this value |
| `AtPath(string)` | JSONPath from this value |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |
| **Number inspection** | |
| `GetNumberType()` | `JsonNumberType` sub-type (float/signed/unsigned/big) |
| `IsNegative()` | `true` if the number is negative |
| `IsInteger()` | `true` if the number has no fractional part |
| **Numbers in strings** | |
| `GetDoubleInString()` | Parse `double` from a quoted number like `"3.14"` |
| `GetInt64InString()` | Parse `long` from a quoted number like `"-42"` |
| `GetUInt64InString()` | Parse `ulong` from a quoted number like `"18446744073709551615"` |
| `TryGetDoubleInString(out double)` | Non-throwing `GetDoubleInString` |
| `TryGetInt64InString(out long)` | Non-throwing `GetInt64InString` |
| `TryGetUInt64InString(out ulong)` | Non-throwing `GetUInt64InString` |
| **Raw JSON** | |
| `GetRawJsonToken()` | Raw token text (includes quotes for strings) |
| `GetRawJsonTokenSpan()` | Zero-allocation `ReadOnlySpan<byte>` of the raw token |
| `GetRawJson()` | Full raw JSON including nested objects/arrays |

### `JsonArray` *(IDisposable, IEnumerable\<JsonValue\>)*

| Member | Description |
|--------|-------------|
| `Count` | Number of elements (full scan) |
| `foreach` | Iterate elements as `JsonValue` |
| `At(int)` | Element at zero-based index via native `array.at()` |
| `ElementAt(int)` | Element at zero-based index via iteration |
| `GetRawJson()` | Full raw JSON of the array (consumes iterator — call `Reset()` to re-iterate) |
| `Reset()` | Reset the array iterator |

### `JsonObject` *(IDisposable, IEnumerable\<JsonProperty\>)*

| Member | Description |
|--------|-------------|
| `Count` | Number of fields (full scan) |
| `GetField(string)` / `this[string]` | Field by name (order-insensitive) |
| `FindField(string)` | Order-sensitive forward-search field lookup |
| `AtPointer(string)` | JSON Pointer from this object |
| `AtPath(string)` | JSONPath from this object |
| `TryGetField(string, out JsonValue?)` | Non-throwing `GetField` |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |
| `ContainsKey(string)` | Returns `true` if the key exists |
| `foreach` | Iterate as `JsonProperty` (`Name` + `Value`) |
| `GetRawJson()` | Full raw JSON of the object (consumes iterator — call `Reset()` to re-iterate) |
| `Reset()` | Reset the object iterator |

### `JsonNumberType`

| Value | Description |
|-------|-------------|
| `FloatingPoint` | A `double` (e.g. `3.14`, `1e10`) |
| `SignedInteger` | A `long` (e.g. `-42`, `0`) |
| `UnsignedInteger` | A `ulong` ≥ 2⁶³ (e.g. `10000000000000000000`) |
| `BigInteger` | An integer outside 64-bit range — read via `GetRawJsonToken()` |

### `JsonValueKind`

`Array`, `Object`, `Number`, `String`, `Boolean`, `Null`, `Unknown`

### `JsonProperty`

| Member | Description |
|--------|-------------|
| `Name` | Unescaped field key (`string`) |
| `Value` | Field value (`JsonValue`) — dispose when done |

### `SimdJsonException`

Thrown on any native error. `ErrorCode` holds the raw bridge error code.

| Code | Meaning |
|------|---------|
| `-1` | Parser capacity exceeded |
| `-2` | Incorrect JSON type |
| `-3` | No such field |
| `-4` | Index out of bounds |
| `-5` | Null pointer passed to bridge |
| `-6` | JSON parse error |
| `-7` | Iteration error |
| `-8` | Invalid JSON pointer |
| `-9` | Scalar document used as value |
| `-99` | Unknown native error |

## Design Notes

- **One parser per thread**: simdjson On-Demand parsers are not thread-safe. Use `SimdJsonParser.Shared` for effortless thread-local access.
- **One document at a time**: a single parser instance can only have one live `JsonDocument`. Dispose the document before parsing the next one.
- **Forward-only iteration**: simdjson On-Demand is a streaming parser. Accessing fields in a different order than they appear in the JSON, or iterating an already-consumed iterator, may throw `SimdJsonException`. Use `GetField`/`this[key]` (order-insensitive) or call `Rewind()`/`Reset()` if you need to re-read.
- **`FindField` vs `GetField`**: `FindField` is order-sensitive and searches forward from the current iterator position — use it when you know you are accessing fields in declaration order. `GetField` (`this[key]`) is order-insensitive and can find fields that appear later in the document.
- **Raw JSON**: `GetRawJson()` on arrays and objects traverses and consumes the iterator. Call `Reset()` afterwards if you need to iterate again.
- **Number types**: use `GetNumberType()` before reading a number to pick the most efficient getter. `BigInteger` values must be read as strings via `GetRawJsonToken()`.
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
