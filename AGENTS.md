# AGENTS — SimdJson.Net

Guidance for AI agents (Codex, Claude, Copilot, etc.) working in this repository.

---

## Project overview

**SimdJson.Net** is a .NET wrapper for [simdjson](https://github.com/simdjson/simdjson) v4.6.11 that exposes the On-Demand streaming JSON parser.

```
simdjson (C++, FetchContent) ──► SimdJsonNative.dll  (C ABI bridge, CMake)
                                         │
                                  [LibraryImport] P/Invoke
                                         │
                                  SimdJson.Net.dll  (C#, public API)
```

### Repository layout

| Path | Role |
|------|------|
| `SimdJson.Net.Native/` | CMake project; compiles the native C ABI bridge |
| `SimdJson.Net.Native/src/simdjson_native.cpp` | All bridge function implementations |
| `SimdJson.Net.Native/include/simdjson_native.h` | C API declarations (C++ and C share this header) |
| `SimdJson.Net/Internal/NativeMethods.cs` | `[LibraryImport]` P/Invoke declarations |
| `SimdJson.Net/Internal/NativeLoader.cs` | Runtime DLL resolution (runtimes/ layout + flat) |
| `SimdJson.Net/*.cs` | Public C# types (`SimdJsonParser`, `JsonDocument`, `JsonValue`, `JsonArray`, `JsonObject`, …) |
| `SimdJson.Net/NdjsonParser.cs` | NDJSON: stream-based reading plus the in-memory `iterate_many` batching API |
| `SimdJson.Net/JsonDocumentStream.cs` | Forward-only stream of documents from one in-memory buffer |
| `SimdJson.Net.Benchmark/` | BenchmarkDotNet suites: parse, field access, full traversal, NDJSON |
| `NupkgValidator/` | Consumes the freshly built package and loads the native library on each RID |
| `.github/workflows/release.yml` | Publishes to NuGet.org and GitHub Packages when a release is published |
| `SimdJson.Net.Tests/*.cs` | TUnit tests, one file per topic (428 per target framework, 1,284 across net8/net9/net10) |
| `Samples/0N-Name/Program.cs` | 13 standalone demo apps |
| `docs/` | Per-type API reference; `docs/API.md` is the index |
| `.github/workflows/build.yml` | CI: builds native libs for 8 RIDs, then runs .NET tests |

---

## Before you start coding

The native library is **not checked in**. `SimdJson.Net/runtimes/` is gitignored. Build it first:

```bash
RID=win-x64  # adjust for your platform
INSTALL="$(pwd)/SimdJson.Net/runtimes/$RID/native"
mkdir -p "$INSTALL"
cmake -S SimdJson.Net.Native -B SimdJson.Net.Native/build \
      -DCMAKE_BUILD_TYPE=Debug \
      -DCMAKE_INSTALL_PREFIX="$INSTALL"
cmake --build   SimdJson.Net.Native/build
cmake --install SimdJson.Net.Native/build
```

Then `dotnet build` and `dotnet test` as usual.

---

## Architecture details

### C ABI bridge

- All C++ objects are heap-allocated and exposed as opaque `void*` handles (`nint` in C#).
- Every "Create/Get/Begin" call has a matching "Destroy" call. The C# `using`/`Dispose` pattern maps directly to these destroy calls.
- Functions return a `SimdJsonError` integer (`0` = success). `SimdJsonException.ThrowIfError(int)` converts non-zero to a managed exception.
- Entry point naming convention: `SimdJsonNative_<PascalCaseName>` (e.g. `SimdJsonNative_ValueGetString`).

### C++ structs in the bridge

```cpp
struct BridgeDocument {
    padded_string json_buf;      // owned input copy; empty when the document is borrowed
    ondemand::document owned;    // storage when this handle owns the document
    ondemand::document* doc;     // the live document: &owned, or one inside a stream
    bool from_stream;            // true for documents handed out by StreamNext
};
struct BridgeStream {            // NDJSON batching: iterate_many
    padded_string json_buf;
    ondemand::document_stream stream;
    ondemand::document_stream::iterator current, end;
    bool started;
};
struct BridgeValue    { ondemand::value value;  };
struct BridgeArray    { ondemand::array array;  };
struct BridgeObject   { ondemand::object object; };
```

`BridgeArrayIterator` and `BridgeObjectIterator` hold iterator + end state for sequential `foreach` iteration.

Two consequences of the `BridgeDocument` shape worth knowing:

- Always go through `bd->doc`, never `bd->owned`, except when assigning the result of `iterate`.
- Root scalar getters must use the `DOC_SCALAR` macro. Stream documents are read through
  `document_reference`, whose getters skip the trailing-content check, because the bytes after a
  stream document are simply the next document. Calling `bd->doc->get_int64()` directly on a stream
  document reports `-13` for every document but the last.

### NativeLoader

Searches for `SimdJsonNative.{dll,so,dylib}` in:
1. `runtimes/<rid>/native/` relative to the assembly, AppBase, and other candidate dirs
2. A flat directory (how NuGet deploys native assets at publish time)
3. Falls back to the OS loader (PATH / LD_LIBRARY_PATH)

---

## simdjson On-Demand — the most important constraint

**On-Demand is a forward-only streaming parser.** This is not a limitation of the wrapper — it is fundamental to how simdjson achieves its performance.

### Rules

| Rule | Consequence of violation |
|------|--------------------------|
| Access fields in document order, or use `GetField`/`this[key]` (order-insensitive) | `SimdJsonException` code `-7` (OUT_OF_ORDER_ITERATION) |
| **Fully consume a nested object/array before accessing the next sibling field** | `SimdJsonException` code `-7` |
| One live `JsonDocument` per `SimdJsonParser` instance at a time | `InvalidOperationException` on the second `Parse` |
| Dispose `JsonValue`/`JsonArray`/`JsonObject` handles promptly | Native handle leak |
| Do not use a `JsonValue`/`JsonArray`/`JsonObject` after its document is disposed | `ObjectDisposedException` |
| Spans returned by any `*Span` getter are valid only until the owning `JsonDocument` is disposed | Dangling data |

C++ exceptions must never cross the `extern "C"` boundary: never rely on `simdjson_result`'s implicit
conversion operator in the bridge (it throws). Always take the value with `.get(out)` and translate the
error code. The same applies in reverse for the managed wildcard callback, whose exceptions are captured
and rethrown after the native call returns.

### The nested-consumption rule — the #1 source of bugs

```csharp
// ❌ BAD: geom is opened but "coordinates" is never read before accessing sibling "properties"
using var geom = feature.GetField("geometry").GetObject();
string type = geom.GetField("type").GetString();
// → SimdJsonException "-7" on the next line because geom still has unconsumed fields
using var props = feature.GetField("properties");

// ✅ GOOD: consume ALL of geometry (including coordinates) before touching properties
using var geomVal = feature.GetField("geometry");
using var geom    = geomVal.GetObject();
string type = geom.GetField("type").GetString();
using var coordsVal = geom.GetField("coordinates");
// ... read coordinates here ...
// geom is now fully consumed — safe to move to next sibling
using var props = feature.GetField("properties");
```

### Parser reuse pitfall

```csharp
// ❌ BAD: Shared is reused while doc is still live
using var doc = SimdJsonParser.Shared.Parse(outerJson);
using var val = doc.GetField("count");
// This parse invalidates doc:
using var doc2 = SimdJsonParser.Shared.Parse("-7");  // doc is now garbage

// ✅ GOOD: separate parser instance for the nested parse
using var mainParser = new SimdJsonParser();
using var doc = mainParser.Parse(outerJson);
using var val = doc.GetField("count");
using var tmp = SimdJsonParser.Shared.Parse("-7");  // separate parser, doc still valid
```

---

## How to add a new feature

### Step 1 — C++ implementation (`simdjson_native.cpp`)

```cpp
extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueMyNewMethod(
    void* value_handle, /* output params */ out_type* out)
{
    if (!value_handle || !out) return SIMDJSON_BRIDGE_ERR_NULL_POINTER;
    auto* v = static_cast<BridgeValue*>(value_handle);
    auto result = v->value.my_simdjson_method();
    if (result.error()) return translate_error(result.error());
    *out = /* convert result.value() */;
    return SIMDJSON_BRIDGE_SUCCESS;
}
```

### Step 2 — Header (`simdjson_native.h`)

```c
extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueMyNewMethod(void* value, out_type* out);
```

### Step 3 — P/Invoke (`NativeMethods.cs`)

```csharp
[LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueMyNewMethod")]
[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
internal static partial int ValueMyNewMethod(nint value, out OutType outVal);
```

### Step 4 — C# wrapper (`JsonValue.cs` or appropriate class)

```csharp
public ReturnType MyNewMethod()
{
    SimdJsonException.ThrowIfError(NativeMethods.ValueMyNewMethod(_handle, out var result));
    return ConvertResult(result);
}
```

### Step 5 — Test (the matching file in `SimdJson.Net.Tests/`)

```csharp
[Test]
public async Task MyNewMethod_Returns_Expected()
{
    using var doc = SimdJsonParser.Shared.Parse("""{"val":42}""");
    using var val = doc.GetField("val");
    await Assert.That(val.MyNewMethod()).IsEqualTo(expectedValue);
}
```

---

## Test framework

This project uses **TUnit** (not xUnit, not NUnit, not MSTest).

```csharp
[Test]
public async Task Test_Name()
{
    // arrange
    using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
    // act
    using var val = doc.GetField("x");
    // assert
    await Assert.That(val.GetInt64()).IsEqualTo(1L);
}
```

Run all tests: `dotnet test SimdJson.Net.Tests`  
Run on a specific TFM: `dotnet test SimdJson.Net.Tests -f net10.0`

---

## Error codes

| Code | C++ error | Meaning |
|------|-----------|---------|
| `0` | `SUCCESS` | OK |
| `-1` | `CAPACITY` | Parser buffer too small |
| `-2` | `INCORRECT_TYPE` | Wrong JSON type for getter |
| `-3` | `NO_SUCH_FIELD` | Field name not found |
| `-4` | `INDEX_OUT_OF_BOUNDS` | Array index past end |
| `-5` | *(bridge)* | Null pointer passed |
| `-6` | `TAPE_ERROR` / parse errors | Malformed JSON |
| `-7` | `OUT_OF_ORDER_ITERATION` / `PARSER_IN_USE` | Forward-only constraint violated |
| `-8` | `INVALID_JSON_POINTER` | Bad RFC 6901 pointer syntax |
| `-9` | `SCALAR_DOCUMENT_AS_VALUE` | Scalar doc used as container |
| `-10` | `NUMBER_OUT_OF_RANGE` / `BIGINT_ERROR` | Number does not fit the requested type |
| `-11` | `MEMALLOC` / `OUT_OF_CAPACITY` | Native allocation failed |
| `-12` | `DEPTH_ERROR` | Nesting deeper than the parser's max depth |
| `-13` | `TRAILING_CONTENT` | Extra content after the JSON value |
| `-14` | `INSUFFICIENT_PADDING` | Buffer lacks the padding `ParseInPlace` requires |
| `-99` | *(unknown)* | Unrecognised simdjson error |

When adding a bridge function, make sure any new simdjson `error_code` it can return is handled in
`translate_error`. Codes without a dedicated mapping are encoded as `-1000` minus the simdjson code,
so `SimdJsonException.Message` can recover simdjson's own text through `SimdJsonNative_ErrorMessage`.
Prefer adding a real mapping for anything a caller might reasonably branch on: the encoded range is
diagnostic output, not API.

---

## Bumping the simdjson version

`<SimdJsonVersion>` in `Directory.Build.props` is the single source of truth for the build: CMake
reads it to pick the git tag, and the package version derives from it. Several places repeat the
number for humans and do **not** update themselves. After changing it, update all of these:

| Location | What to change |
|----------|----------------|
| `Directory.Build.props` | `<SimdJsonVersion>` — the only one that affects the build |
| `README.md` | the simdjson badge, its release link, the intro line, and the `GetVersion()` comment |
| `AGENTS.md`, `.github/copilot-instructions.md` | the version in the project-overview line |
| `docs/API.md`, `docs/SimdJsonParser.md` | the `GetVersion()` example output |
| `SimdJson.Net/SimdJsonParser.cs` | the XML doc example on `GetVersion` |

`grep -rn "<old version>" --include=*.md --include=*.props --include=*.cs .` finds the lot.

Read the upstream release notes between the old and new versions before bumping. Several past
releases changed behaviour this wrapper depends on, and the pull request should say which.

---

## CI / build pipeline

`.github/workflows/build.yml`:
1. **Parallel native builds** — 8 RIDs (win-x64, win-arm64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-x64, osx-arm64). Each step: `cmake -S … -DCMAKE_INSTALL_PREFIX=… && cmake --build … && cmake --install …`
2. **Artifact upload** — each native build uploads its `runtimes/<rid>/native/` folder.
3. **Test job** — downloads all 8 artifacts, runs `dotnet test` on net8/net9/net10 for each RID.

When modifying the CMake build, update both `CMakeLists.txt` and `CMakePresets.json`. The preset's `CMAKE_INSTALL_PREFIX` must match the expected runtimes path.

---

## Coding conventions

- **No new docstrings/comments** unless the changed code genuinely needs explanation.
- **No extra abstractions** for one-time operations.
- **`using var`** everywhere for disposable native handles.
- **`ThrowIfError`** immediately after every P/Invoke call.
- **String passing to native**: `fixed (byte* p = MemoryMarshal.AsBytes(key.AsSpan()))` — keys are passed as UTF-8 byte pointers with a length. See existing `GetField` implementations.
- **Key matching is against escaped bytes**: simdjson compares a lookup key with the raw bytes in the document, so `GetField("a\"b")` never matches `{"a\"b":1}`. Object iteration exposes both forms, `JsonProperty.Name` unescaped and `JsonProperty.EscapedName` as written; only the latter round-trips into a lookup.
- **Target frameworks**: `net8.0;net9.0;net10.0` — all three must stay in sync.
- The experimental `ParseAllowIncompleteJson` path is guarded by `#ifdef SIMDJSON_EXPERIMENTAL_ALLOW_INCOMPLETE_JSON` in the C++ bridge; this define is set via `add_compile_definitions` in `CMakeLists.txt`.

---

## Key files to read before making changes

| Task | Files to read first |
|------|---------------------|
| Adding a new scalar getter | `SimdJson.Net/JsonValue.cs`, `Internal/NativeMethods.cs`, `SimdJson.Net.Native/src/simdjson_native.cpp` |
| Adding array/object features | `SimdJson.Net/JsonArray.cs` or `JsonObject.cs`, corresponding bridge structs in `simdjson_native.cpp` |
| Changing DLL loading | `Internal/NativeLoader.cs` |
| Changing CI | `.github/workflows/build.yml`, `SimdJson.Net.Native/CMakePresets.json` |
| Adding a sample | `Samples/` — copy an existing project, follow the `0N-Name` naming, add `<ProjectReference>` to `SimdJson.Net.csproj` |
