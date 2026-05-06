# `JsonDocument` *(IDisposable)*

The root result of a `SimdJsonParser.Parse` call. Holds a native handle to the parsed document and an On-Demand iterator positioned at the root.

> **Always dispose**: every `JsonDocument` holds a native handle. Use `using var doc = ...`.

## Type inspection

| Member | Description |
|--------|-------------|
| `ValueKind` | `JsonValueKind` of the document root |
| `IsScalar()` | `true` if the root is not an array or object |
| `IsString()` | `true` if the root is a JSON string |

## Root access

| Member | Description |
|--------|-------------|
| `GetArray()` | Root as [`JsonArray`](JsonArray.md) (throws if root is not an array) |
| `GetObject()` | Root as [`JsonObject`](JsonObject.md) (throws if root is not an object) |
| `GetValue()` | Root as [`JsonValue`](JsonValue.md) (scalar roots only) |

## Field / pointer lookup *(root must be an object)*

| Member | Description |
|--------|-------------|
| `GetField(string)` / `this[string]` | Field by name — order-insensitive |
| `FindField(string)` | Order-sensitive forward-search field lookup |
| `AtPointer(string)` | RFC 6901 JSON Pointer (e.g. `"/items/0/name"`) |
| `AtPath(string)` | JSONPath expression (e.g. `"$.items[0].name"`) |
| `TryGetField(string, out JsonValue?)` | Non-throwing `GetField` |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |

All lookup methods return a `JsonValue` that **must be disposed**.

## Iterator control

| Member | Description |
|--------|-------------|
| `Rewind()` | Reset the document iterator to the beginning |

Call `Rewind()` when you need to access the document more than once (e.g. read one field, then re-iterate from the start).

## Raw JSON & diagnostics

| Member | Description |
|--------|-------------|
| `GetRawJson()` | Full raw JSON of the document root as a `string` |
| `GetWobblyStringSpan()` | Root string as WTF-8 `ReadOnlySpan<byte>` (allows lone surrogates) |
| `CurrentOffset()` | Byte offset of the current parse position from the document start |
| `CurrentDepth()` | Current JSON nesting depth (`0` = root level) |

## Examples

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice","age":30}""");

// Access root as object fields
using var name = doc.GetField("name");
Console.WriteLine(name.GetString()); // Alice

// JSON Pointer
using var val = doc.AtPointer("/address/city");

// Iterate root object fields
using var obj = doc.GetObject();
foreach (var prop in obj)
{
    Console.WriteLine($"{prop.Name}: {prop.Value.ValueKind}");
    prop.Value.Dispose();
}

// Parse a JSON array at root
using var arrDoc = SimdJsonParser.Shared.Parse("[1,2,3]");
using var arr = arrDoc.GetArray();
foreach (var item in arr)
{
    Console.WriteLine(item.GetInt64());
    item.Dispose();
}

// Rewind and read again
doc.Rewind();
using var name2 = doc.GetField("name");
```

---

← [API Reference](API.md)
