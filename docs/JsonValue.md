# `JsonValue` *(IDisposable)*

Represents a JSON value at a specific position in an On-Demand document. Can be a scalar (string, number, bool, null) or a container (array, object).

> **Always dispose**: every `JsonValue` holds a native handle. Use `using var v = ...`.

## Type inspection

| Member | Description |
|--------|-------------|
| `ValueKind` | [`JsonValueKind`](Types.md#jsonvaluekind) of this value |
| `IsScalar()` | `true` if this value is not an array or object |
| `IsString()` | `true` if this value is a JSON string |
| `IsNull()` | `true` if the value is JSON `null` |

## Scalar getters

| Member | Description |
|--------|-------------|
| `GetString()` | As a managed `string` |
| `GetStringSpan()` | As `ReadOnlySpan<byte>` — zero allocation |
| `GetDouble()` | As `double` |
| `GetFloat()` | As `float` |
| `GetDecimal()` | As `decimal` |
| `GetInt64()` | As `long` |
| `GetUInt64()` | As `ulong` |
| `GetInt32()` | As `int` |
| `GetBool()` | As `bool` |

## Container access

| Member | Description |
|--------|-------------|
| `GetArray()` | As [`JsonArray`](JsonArray.md) (throws if not an array) |
| `GetObject()` | As [`JsonObject`](JsonObject.md) (throws if not an object) |

## Field / pointer lookup *(value must be an object)*

| Member | Description |
|--------|-------------|
| `GetField(string)` / `this[string]` | Field by name — order-insensitive |
| `FindField(string)` | Order-sensitive forward-search field lookup |
| `AtPointer(string)` | JSON Pointer from this value |
| `AtPath(string)` | JSONPath from this value |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |

## Number inspection

Prefer `GetNumber()` when you need both the type and the value in one call.

| Member | Description |
|--------|-------------|
| `GetNumberType()` | [`JsonNumberType`](Numbers.md#jsonnumbertype) sub-type |
| `IsNegative()` | `true` if the number is negative |
| `IsInteger()` | `true` if the number has no fractional part |
| `GetNumber()` | Full typed number as [`JsonNumber`](Numbers.md#jsonnumber) struct |

## Numbers in strings

For JSON APIs that encode numbers as quoted strings (e.g. `"price": "9.99"`).

| Member | Description |
|--------|-------------|
| `GetDoubleInString()` | Parse `double` from a quoted number |
| `GetInt64InString()` | Parse `long` from a quoted number |
| `GetUInt64InString()` | Parse `ulong` from a quoted number |
| `TryGetDoubleInString(out double)` | Non-throwing `GetDoubleInString` |
| `TryGetInt64InString(out long)` | Non-throwing `GetInt64InString` |
| `TryGetUInt64InString(out ulong)` | Non-throwing `GetUInt64InString` |

## Raw JSON & diagnostics

## Wildcard path iteration

| Member | Description |
|--------|-------------|
| `ForEachAtPath(string path, Action<JsonValue> callback)` | Invoke `callback` for each value matching a JSONPath wildcard expression (e.g. `"$[*]"`, `"$.items[*].name"`, `"$.*"`) starting from this value (must be an array or object). The `JsonValue` passed to the callback is **borrowed** — valid only during the callback, must not be disposed or stored. |

| Member | Description |
|--------|-------------|
| `GetRawJsonToken()` | Raw token text as a `string` (includes quotes for strings) |
| `GetRawJsonTokenSpan()` | Raw token as `ReadOnlySpan<byte>` — zero allocation |
| `GetRawJson()` | Full raw JSON including nested objects/arrays |
| `GetRawJsonString()` | Raw escaped bytes of a string value as a `string`, no surrounding quotes |
| `GetRawJsonStringSpan()` | Same as above as `ReadOnlySpan<byte>` — zero allocation |
| `GetWobblyStringSpan()` | String as WTF-8 bytes (allows lone surrogates) |
| `CurrentOffset(JsonDocument)` | Byte offset of current parse position in the document |
| `CurrentDepth(JsonDocument)` | Current JSON nesting depth (`0` = root level) |

## Examples

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"tag":"hot","price":9.99,"count":42}""");

// Scalar reads
using var tag   = doc.GetField("tag");
using var price = doc.GetField("price");
using var count = doc.GetField("count");

Console.WriteLine(tag.GetString());          // hot
Console.WriteLine(price.GetDouble());        // 9.99
Console.WriteLine(count.GetInt64());         // 42

// Zero-allocation span comparison
ReadOnlySpan<byte> expected = "hot"u8;
Console.WriteLine(tag.GetStringSpan().SequenceEqual(expected)); // True

// Number type dispatch
using var n = doc.GetField("price");
switch (n.GetNumberType())
{
    case JsonNumberType.FloatingPoint: Console.WriteLine(n.GetDouble()); break;
    case JsonNumberType.SignedInteger: Console.WriteLine(n.GetInt64());  break;
}

// Quoted number
using var doc2 = SimdJsonParser.Shared.Parse("""{"amount":"3.14"}""");
using var amt  = doc2.GetField("amount");
double d = amt.GetDoubleInString(); // 3.14

// Nested access
using var doc3 = SimdJsonParser.Shared.Parse("""{"a":{"b":{"c":99}}}""");
using var c    = doc3["a"]["b"]["c"];
Console.WriteLine(c.GetInt64()); // 99
```

---

← [API Reference](API.md)
