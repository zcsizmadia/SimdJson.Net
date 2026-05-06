# API Reference

Complete reference for the `SimdJson.Net` public API.

## Classes & types

| Type | Description |
|------|-------------|
| [SimdJsonParser](SimdJsonParser.md) | Main entry point — parse JSON, static utilities |
| [JsonDocument](JsonDocument.md) | Root result of a parse call; holds the document iterator |
| [JsonValue](JsonValue.md) | A single JSON value — scalar, array, or object |
| [JsonArray](JsonArray.md) | A JSON array with iteration and index access |
| [JsonObject](JsonObject.md) | A JSON object with field lookup and iteration |
| [Numbers](Numbers.md) | `JsonNumberType` enum and `JsonNumber` struct |
| [Types & Errors](Types.md) | `JsonValueKind`, `JsonProperty`, `SimdJsonException` error codes |

## Key concepts

- **Parsing**: call `SimdJsonParser.Shared.Parse(json)` or `new SimdJsonParser().ParseAsync(stream)`.
- **Forward-only iterator**: simdjson On-Demand processes JSON in a single pass. Fields must be consumed in order, or use `GetField`/`this[key]` for order-insensitive lookup.
- **Dispose everything**: `JsonDocument`, `JsonValue`, `JsonArray`, and `JsonObject` all hold native handles. Always wrap in `using`.
- **One document per parser**: a parser instance can only hold one live document at a time.

For patterns, pitfalls, and detailed rules see [Design Notes](DesignNotes.md).

## Quick lookup

### Parsing

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
using var doc = SimdJsonParser.Shared.Parse(utf8Span);
using var doc = await parser.ParseAsync(stream, ct);
```

### Scalar access

```csharp
using var v = doc.GetField("name");
v.GetString();     v.GetStringSpan();
v.GetInt64();      v.GetUInt64();    v.GetDouble();
v.GetBool();       v.IsNull();
```

### Arrays

```csharp
using var arr = doc.GetArray();          // or val.GetArray()
foreach (var item in arr) { ...; item.Dispose(); }
using var el = arr.At(0);               // by index
Console.WriteLine(arr.Count);
Console.WriteLine(arr.IsEmpty());
```

### Objects

```csharp
using var obj = doc.GetObject();         // or val.GetObject()
using var val = obj.GetField("key");     // order-insensitive
obj.TryGetField("opt", out var opt);
foreach (var prop in obj) { ...; prop.Value.Dispose(); }
```

### Pointers & paths

```csharp
using var v = doc.AtPointer("/store/items/0/name"); // RFC 6901
using var v = doc.AtPath("$.store.items[0].name");  // JSONPath
doc.TryAtPointer("/optional", out var v);
```

### Numbers

```csharp
JsonNumber n = val.GetNumber();
// n.NumberType, n.AsDouble(), n.AsInt64(), n.AsUInt64()
```

### Utilities

```csharp
SimdJsonParser.GetVersion();           // "4.6.3"
SimdJsonParser.Minify(json);
SimdJsonParser.ValidateUtf8(bytes);
```
