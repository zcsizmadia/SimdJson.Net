# Design Notes

## One parser per thread

`SimdJsonParser` is not thread-safe. Every thread must have its own instance. Use `SimdJsonParser.Shared` for transparent thread-local access without managing lifetimes:

```csharp
// Safe: Shared is a [ThreadStatic]-backed instance
using var doc = SimdJsonParser.Shared.Parse(json);
```

Do not dispose `SimdJsonParser.Shared`.

---

## One document at a time

A single `SimdJsonParser` can only have one live `JsonDocument` at any moment. Parsing a second document on the same instance invalidates the first:

```csharp
// BAD — doc1 is invalidated when doc2 is parsed
using var parser = new SimdJsonParser();
using var doc1   = parser.Parse(json1);
using var doc2   = parser.Parse(json2); // doc1 is now invalid

// GOOD — use separate parsers, or dispose before reusing
using var parser = new SimdJsonParser();
using (var doc1 = parser.Parse(json1))
{
    // use doc1 fully
}
using var doc2 = parser.Parse(json2); // safe
```

When using `SimdJsonParser.Shared`, the same constraint applies. If you need to parse a second document while the first is still alive (e.g. for number-in-string helpers), create a separate `new SimdJsonParser()` instance.

---

## Forward-only iteration (On-Demand)

simdjson On-Demand is a streaming parser. Its iterator moves forward through the JSON bytes and cannot go back unless you explicitly call `Rewind()` or `Reset()`.

**Rules:**
1. Access fields in the order they appear in the JSON, or use `GetField`/`this[key]` (order-insensitive) which handles reordering internally.
2. Fully consume a nested object or array before accessing the next sibling field in its parent.
3. Dispose intermediate `JsonValue` objects as soon as you are done with them.
4. Call `Rewind()` on `JsonDocument` or `Reset()` on `JsonArray` / `JsonObject` to re-iterate from the start.

**Example of the nested consumption rule:**

```csharp
// BAD — geometry object is not fully consumed before accessing properties
using var geom     = feature.GetField("geometry").GetObject();
using var geomType = geom.GetField("type");
string type = geomType.GetString();
// ❌ geometry's "coordinates" field was never read → iterator error
using var props = feature.GetField("properties"); // SimdJsonException: Iteration error

// GOOD — fully consume geometry (including coordinates) first
using var geomVal  = feature.GetField("geometry");
using var geom     = geomVal.GetObject();
using var typeVal  = geom.GetField("type");
string type = typeVal.GetString();
using var coordsVal = geom.GetField("coordinates");
// ... read coordinates ...
// Now geometry is fully consumed — safe to access sibling
using var props = feature.GetField("properties"); // ✅
```

See [sample 09](../Samples/09-RealWorld-GeoJson/Program.cs) for a complete real-world example.

---

## `GetField` vs `FindField`

| Method | Order | Notes |
|--------|-------|-------|
| `GetField(key)` / `this[key]` | Order-insensitive | Can find fields that appear later in the document; may internally rewind |
| `FindField(key)` | Order-sensitive | Searches forward from the current position; faster if field order matches JSON |
| `FindFieldUnordered(key)` | Order-insensitive | Alias for `GetField` |

Use `GetField` unless you know the field order matches the JSON and you need maximum throughput.

---

## `GetRawJson` and iterator consumption

`GetRawJson()` on `JsonArray` and `JsonObject` traverses the entire subtree to collect the raw JSON text, which consumes the iterator. Call `Reset()` afterwards if you need to iterate again:

```csharp
using var arr = doc.GetArray();
string raw = arr.GetRawJson(); // consumes iterator
arr.Reset();                   // re-position at start
foreach (var item in arr) { ... }
```

---

## `IsEmpty()` vs `Count == 0`

`JsonArray.IsEmpty()` peeks at the first element without advancing the iterator and is O(1). `Count` performs a full scan. Prefer `IsEmpty()` when you only need a presence check.

---

## Parser capacity

Use `new SimdJsonParser(maxCapacity)` or set `parser.MaxCapacity` to enforce a size limit (e.g. to prevent oversized documents from consuming memory). `parser.Capacity` reflects the current internal buffer size after at least one parse call.

---

## `CurrentDepth` / `CurrentOffset`

Useful for building error messages that point to the location in the JSON where a problem was detected:

```csharp
try
{
    using var val = doc.AtPointer("/users/0/id");
}
catch (SimdJsonException ex)
{
    Console.WriteLine($"Error at byte {doc.CurrentOffset()}, depth {doc.CurrentDepth()}: {ex.Message}");
}
```

---

## WTF-8 strings

`GetWobblyStringSpan()` returns raw UTF-8 bytes that may contain lone Unicode surrogates. Only use this API when round-tripping JSON produced by runtimes that encode surrogates non-standardly (e.g. JavaScript `JSON.stringify` on strings with unpaired surrogates). For normal use, prefer `GetString()` or `GetStringSpan()`.

---

## Dispose discipline

Every `JsonDocument`, `JsonValue`, `JsonArray`, and `JsonObject` wraps a native handle. Failing to dispose creates a resource leak. Always use `using` statements:

```csharp
using var doc  = SimdJsonParser.Shared.Parse(json);
using var val  = doc.GetField("key");
using var arr  = doc.GetArray();
```

Within `foreach` loops, dispose each element explicitly:

```csharp
foreach (var item in arr)
{
    // use item
    item.Dispose(); // required before next iteration
}
```

---

← [API Reference](API.md)
