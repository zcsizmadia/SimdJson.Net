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
| `GetRawJsonSpan()` | Full raw JSON as a zero-allocation `ReadOnlySpan<byte>` |
| `GetWobblyStringSpan()` | Root string as WTF-8 `ReadOnlySpan<byte>` (allows lone surrogates) |
| `CurrentOffset()` | Byte offset of the current parse position from the document start |
| `CurrentDepth()` | Current JSON nesting depth (`0` = root level) |

## Root scalar getters

Use when the JSON document root is a bare scalar value (e.g. `"hello"`, `42`, `true`, `null`).

| Member | Description |
|--------|-------------|
| `GetString()` | Root as `string` (rejects lone surrogates) |
| `GetString(bool allowReplacement)` | Root as `string`; `allowReplacement: true` replaces lone surrogates with U+FFFD |
| `GetStringSpan()` | Root as UTF-8 `ReadOnlySpan<byte>`, zero-allocation |
| `GetBool()` | Root as `bool` |
| `IsNull()` | `true` when the root is `null` |
| `GetDouble()` | Root as `double` |
| `GetInt64()` | Root as `long` |
| `GetUInt64()` | Root as `ulong` |
| `GetDoubleInString()` | Parse a `double` out of a root JSON string (e.g. `"3.14"`) |
| `GetInt64InString()` | Parse a `long` out of a root JSON string (e.g. `"-99"`) |
| `GetUInt64InString()` | Parse a `ulong` out of a root JSON string (e.g. `"100"`) |
| `At(int index)` | Element at 0-based `index` when root is an array — returns a `JsonValue` that must be disposed |

## Counting

| Member | Description |
|--------|-------------|
| `CountElements()` | Number of elements when root is an array (full scan, exhausts iterator) |
| `CountFields()` | Number of fields when root is an object (full scan, exhausts iterator) |

> After calling `CountElements`/`CountFields` the iterator is exhausted. Call `Rewind()` to reset.

## Wildcard path iteration

| Member | Description |
|--------|-------------|
| `ForEachAtPath(string path, Action<JsonValue> callback)` | Invoke `callback` for each value matching a JSONPath wildcard expression. The document is rewound automatically before iteration. The `JsonValue` passed to the callback is **borrowed** — valid only during the callback, must not be disposed or stored. |

Supported path patterns:

| Pattern | Matches |
|---------|---------|
| `$[*]` | Every element of the root array |
| `$.*` | Every field value of the root object |
| `$.items[*]` | Every element of the `items` array |
| `$.items[*].name` | The `name` field of every element in `items` |

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
