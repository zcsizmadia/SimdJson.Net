# `JsonObject` *(IDisposable, IEnumerable\<JsonProperty\>)*

Represents a JSON object obtained from a parsed document. Wraps a forward-only On-Demand object iterator.

> **Always dispose**: every `JsonObject` holds a native handle. Use `using var obj = ...`.

## Properties

| Member | Description |
|--------|-------------|
| `Count` | Number of fields — performs a full scan |

## Field lookup

| Member | Description |
|--------|-------------|
| `GetField(string)` / `this[string]` | Field by name — order-insensitive; can find fields appearing later in the document |
| `FindField(string)` | Order-sensitive; searches forward from the current iterator position — use when accessing fields in declaration order |
| `FindFieldUnordered(string)` | Alias for `GetField` (order-insensitive) |
| `TryGetField(string, out JsonValue?)` | Non-throwing `GetField` |
| `ContainsKey(string)` | Returns `true` if the key exists (does not return a value) |

All field lookup methods return a [`JsonValue`](JsonValue.md) that **must be disposed**.

## Iteration

```csharp
using var obj = doc.GetObject();
foreach (var prop in obj)
{
    Console.WriteLine($"{prop.Name} = {prop.Value.ValueKind}");
    prop.Value.Dispose(); // required
}
```

Each `JsonProperty` exposes `Name` (string) and `Value` (JsonValue). The `Value` **must be disposed** before the next iteration step.

## Pointer / path lookup

| Member | Description |
|--------|-------------|
| `AtPointer(string)` | JSON Pointer from this object (e.g. `"/address/city"`) |
| `AtPath(string)` | JSONPath from this object (e.g. `"$.address.city"`) |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |

## Iterator control & raw JSON

| Member | Description |
|--------|-------------|
| `Reset()` | Reset the object iterator to the beginning |
| `GetRawJson()` | Full raw JSON of the object as a `string` — consumes the iterator; call `Reset()` to re-iterate |
| `GetRawJsonSpan()` | Full raw JSON as a zero-allocation `ReadOnlySpan<byte>` — also consumes the iterator |

## Wildcard path iteration

| Member | Description |
|--------|-------------|
| `ForEachAtPath(string path, Action<JsonValue> callback)` | Invoke `callback` for each value matching a JSONPath wildcard expression (e.g. `"$.*"`, `"$.items[*].name"`) starting from this object. The `JsonValue` passed to the callback is **borrowed** — valid only during the callback, must not be disposed or stored. |

## Examples

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"host":"localhost","port":5432,"tls":true}""");

// GetField (order-insensitive)
using var obj  = doc.GetObject();
using var host = obj.GetField("host");
using var port = obj.GetField("port");
Console.WriteLine($"{host.GetString()}:{port.GetInt64()}"); // localhost:5432

// TryGetField — no exception on missing keys
if (obj.TryGetField("timeout", out var timeout))
{
    using (timeout) Console.WriteLine(timeout!.GetInt64());
}

// ContainsKey
Console.WriteLine(obj.ContainsKey("tls")); // True

// foreach — inspect all fields
obj.Reset();
foreach (var prop in obj)
{
    Console.WriteLine($"{prop.Name}: {prop.Value.ValueKind}");
    prop.Value.Dispose();
}

// FindField — order-sensitive, faster when field order matches JSON
obj.Reset();
using var h2 = obj.FindField("host"); // found because host comes first
```

> **`GetField` vs `FindField`**: use `GetField` (or the indexer) for safety. Use `FindField` only when you know the field order matches the JSON and performance matters.

---

← [API Reference](API.md)
