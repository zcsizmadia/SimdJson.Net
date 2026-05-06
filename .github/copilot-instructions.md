# GitHub Copilot Instructions — SimdJson.Net

## What this project is

A .NET wrapper for [simdjson](https://github.com/simdjson/simdjson) v4.6.3 exposing the **On-Demand** API. Architecture:

```
C++ (simdjson On-Demand) → C ABI bridge (SimdJsonNative.dll) → C# P/Invoke → public API
```

- **`SimdJson.Net.Native/`** — CMake project that compiles `SimdJsonNative.dll/.so/.dylib` from source via `FetchContent`. The output name is always `SimdJsonNative` regardless of folder name.
- **`SimdJson.Net/`** — The .NET library. `Internal/NativeMethods.cs` holds `[LibraryImport]` P/Invokes. `Internal/NativeLoader.cs` resolves the native DLL at runtime.
- **`SimdJson.Net.Tests/`** — TUnit test project. **Do not use xUnit or NUnit.**
- **`Samples/`** — 10 standalone `net10.0` console apps (one per API area).
- **`docs/`** — Per-type API reference. `docs/API.md` is the index.

## Build requirement

`SimdJson.Net/runtimes/` is **not** checked in. Before building or testing the .NET project, the native library must be produced via:

```bash
cmake -S SimdJson.Net.Native -B SimdJson.Net.Native/build -DCMAKE_INSTALL_PREFIX="$(pwd)/SimdJson.Net/runtimes/win-x64/native"
cmake --build   SimdJson.Net.Native/build
cmake --install SimdJson.Net.Native/build
```

## Critical simdjson On-Demand rules

These are hard constraints from the underlying C++ library — violating them causes `SimdJsonException` with code `-7` (iteration error).

1. **Forward-only**: the document iterator moves in one direction. Use `GetField`/`this[key]` (order-insensitive) not `FindField` (order-sensitive) unless you know the exact field order.
2. **Fully consume nested containers before accessing siblings**: if you open a nested `JsonObject` or `JsonArray`, you must read **all** its content before accessing the next sibling field of the parent. Dispose the nested handles before moving on.
3. **One document per parser at a time**: parsing a second document on the same `SimdJsonParser` instance invalidates the first document.
4. **`SimdJsonParser.Shared` is thread-local**: safe across threads but still subject to rule 3 — don't hold a document and call `Shared.Parse` again on the same thread.

## Dispose everything

`JsonDocument`, `JsonValue`, `JsonArray`, `JsonObject` all own native handles. Always wrap in `using`. In `foreach` loops, call `item.Dispose()` at the end of each iteration body.

## Adding a new API function

1. **C++ (`SimdJson.Net.Native/src/simdjson_native.cpp`)**: implement the function; follow the existing `BridgeDocument`/`BridgeValue`/`BridgeArray`/`BridgeObject` pattern; return a `SimdJsonError` int.
2. **Header (`SimdJson.Net.Native/include/simdjson_native.h`)**: declare with `SIMDJSONNATIVE_API` and `__cdecl`.
3. **P/Invoke (`SimdJson.Net/Internal/NativeMethods.cs`)**: add a `[LibraryImport]` binding; entry point naming is `SimdJsonNative_<FunctionName>`.
4. **C# wrapper**: expose through the appropriate class (`JsonValue`, `JsonObject`, etc.) calling `SimdJsonException.ThrowIfError(...)`.
5. **Test**: add a `[Test]` method in `SimdJson.Net.Tests/SimdJsonTests.cs` using TUnit assertions (`await Assert.That(...)`).

## Test framework: TUnit

```csharp
[Test]
public async Task MyTest()
{
    using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
    using var val = doc.GetField("x");
    await Assert.That(val.GetInt64()).IsEqualTo(1L);
}
```

Run tests: `dotnet test SimdJson.Net.Tests`

## Target frameworks

`net8.0`, `net9.0`, `net10.0` — all three must be kept in sync.

## Experimental incomplete-JSON parsing

`ParseAllowIncompleteJson` is enabled via `add_compile_definitions(SIMDJSON_EXPERIMENTAL_ALLOW_INCOMPLETE_JSON)` in `CMakeLists.txt` and guarded by `#ifdef` in the C++ bridge. Do not remove this define.
