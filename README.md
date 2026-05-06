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

## Documentation

Full API reference and design notes live in the [`docs/`](docs/) folder:

| Document | Contents |
|----------|----------|
| [docs/API.md](docs/API.md) | API index, quick-lookup cheatsheet |
| [docs/SimdJsonParser.md](docs/SimdJsonParser.md) | Parser — `Parse`, `ParseAsync`, utilities |
| [docs/JsonDocument.md](docs/JsonDocument.md) | Document root — field access, pointers, rewind |
| [docs/JsonValue.md](docs/JsonValue.md) | Value — scalar getters, numbers, raw JSON |
| [docs/JsonArray.md](docs/JsonArray.md) | Array — iteration, index access, `Count`, `IsEmpty` |
| [docs/JsonObject.md](docs/JsonObject.md) | Object — `GetField`, `FindField`, `ContainsKey`, iteration |
| [docs/Numbers.md](docs/Numbers.md) | `JsonNumberType`, `JsonNumber` struct |
| [docs/Types.md](docs/Types.md) | `JsonValueKind`, `JsonProperty`, `SimdJsonException` error codes |
| [docs/DesignNotes.md](docs/DesignNotes.md) | Thread safety, forward-only iteration, dispose rules, pitfalls |

## Samples

The `Samples/` folder contains runnable console projects, each demonstrating a different part of the API. After building the native library (see [Building the Native Library Locally](#building-the-native-library-locally)), run any sample with `dotnet run --project Samples/<name>`.

| Sample | What it covers |
|--------|----------------|
| [01-BasicParsing](Samples/01-BasicParsing/Program.cs) | `Parse(string)`, `Parse(ReadOnlySpan<byte>)`, scalar field reads, indexer syntax, `GetVersion()` |
| [02-ArrayIteration](Samples/02-ArrayIteration/Program.cs) | `foreach`, `At(index)`, `ElementAt`, `Count`, `IsEmpty`, nested arrays, array of objects |
| [03-ObjectIteration](Samples/03-ObjectIteration/Program.cs) | `foreach JsonProperty`, `GetField`/`FindField`/`FindFieldUnordered`, `TryGetField`, `ContainsKey`, dynamic `ValueKind` switch |
| [04-JsonPointerAndPath](Samples/04-JsonPointerAndPath/Program.cs) | RFC 6901 `AtPointer`, `AtPath`, `TryAtPointer`/`TryAtPath` on document / value / array / object |
| [05-NumberTypes](Samples/05-NumberTypes/Program.cs) | `GetNumberType`, `GetNumber` → `JsonNumber`, `IsNegative`/`IsInteger`, `GetRawJsonToken`, numbers-in-strings helpers |
| [06-StreamParsing](Samples/06-StreamParsing/Program.cs) | `ParseAsync(Stream)`, `ParseAsync(string)`, `FileStream`, `CancellationToken` |
| [07-ZeroAllocation](Samples/07-ZeroAllocation/Program.cs) | `GetStringSpan`, UTF-8 literal parse, `ReadOnlySpan<byte>` comparison, `GetRawJsonTokenSpan`, `Minify`, `ValidateUtf8` |
| [08-ErrorHandling](Samples/08-ErrorHandling/Program.cs) | `SimdJsonException` error codes, lazy On-Demand parse errors, `TryXxx` non-throwing API, `ParseAllowIncompleteJson` |
| [09-RealWorld-GeoJson](Samples/09-RealWorld-GeoJson/Program.cs) | GeoJSON FeatureCollection — nested objects/arrays, bounding-box calculation, On-Demand forward-iteration discipline |
| [10-RealWorld-LogParser](Samples/10-RealWorld-LogParser/Program.cs) | NDJSON log stream — parser reuse across lines, aggregation, `TryGetField` for optional fields |
| [11-WildcardAndRawString](Samples/11-WildcardAndRawString/Program.cs) | `ForEachAtPath` wildcard JSONPath iteration; `GetRawJsonString`/`GetRawJsonStringSpan` for escaped-byte access |

> **On-Demand iteration tip**: simdjson On-Demand is a forward-only streaming parser. Always fully consume a nested object or array before accessing the next sibling field in its parent. See samples 09 and 10 for patterns.

## Building the Native Library Locally

The `runtimes/` folder is not checked in. After cloning, you need to build the native library before the .NET project will work.

### Windows (Visual Studio 2022)

Open the solution — the `SimdJson.Net.Native/` solution folder contains the CMake project. VS 2022 detects `CMakePresets.json` automatically.

1. Select the **Debug (win-x64)** preset in the CMake toolbar (top of the IDE).
2. **Build → Build All** to compile the native library.
3. **Build → Install SimdJsonNative** to run `cmake --install` — this writes `SimdJsonNative.dll` directly into `SimdJson.Net/runtimes/win-x64/native/`.
4. Build / run the .NET project normally.

> The install step is required once after every CMake rebuild. Steps 2 and 3 can also be triggered by right-clicking `CMakeLists.txt` in Solution Explorer.

### Command Line (all platforms)

```bash
RID=win-x64   # or linux-x64, osx-arm64, etc.
INSTALL_DIR="$(pwd)/SimdJson.Net/runtimes/$RID/native"
mkdir -p "$INSTALL_DIR"

cmake -S SimdJson.Net.Native -B SimdJson.Net.Native/build \
  -DCMAKE_BUILD_TYPE=Debug \
  -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR"
cmake --build   SimdJson.Net.Native/build
cmake --install SimdJson.Net.Native/build
```

Then `dotnet build` / `dotnet test` as normal.

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

