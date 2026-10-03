# `JsonObject` *(IDisposable, IEnumerable\<JsonProperty\>)*

Represents a JSON object obtained from a parsed document. Wraps a forward-only On-Demand object iterator.

> **Always dispose**: every `JsonObject` holds a native handle. Use `using var obj = ...`.

## Properties

| Member | Description |
|--------|-------------|
| `Count` | Number of fields — performs a full scan |
| `IsEmpty()` | `true` if the object has no fields, without the full scan `Count` needs |

## Field lookup

| Member | Description |
|--------|-------------|
| `GetField(string)` / `this[string]` | Field by name — order-insensitive; can find fields appearing later in the document |
| `FindField(string)` | Order-sensitive; searches forward from the current iterator position — use when accessing fields in declaration order |
| `TryFindField(string, out JsonValue?)` | Order-sensitive optional lookup; restores its starting position if the field is missing |
| `FindFieldUnordered(string)` | Alias for `GetField` (order-insensitive) |
| `TryGetField(string, out JsonValue?)` | Non-throwing `GetField` |
| `TryFindFieldUnordered(string, out JsonValue?)` | Non-throwing `FindFieldUnordered` |
| `ContainsKey(string)` | Returns `true` if the key exists (does not return a value) |

Successful lookups return a [`JsonValue`](JsonValue.md) that **must be disposed**. The `Try` methods set the value to `null` when they return `false`.

## Iteration

```csharp
using var obj = doc.GetObject();
foreach (var prop in obj)
{
    Console.WriteLine($"{prop.Name} = {prop.Value.ValueKind}");
    prop.Value.Dispose(); // required
}
```

Each `JsonProperty` exposes `Name` (string), `EscapedName` (string), `EscapedNameSpan` (`ReadOnlySpan<byte>`) and `Value` (JsonValue). The `Value` **must be disposed** before the next iteration step.

`Name` has escape sequences resolved and is what you want for display. `EscapedName` is the key exactly as it appears in the JSON text, and is the form to pass back to `GetField` or `FindField`, because simdjson matches against the document's raw bytes. The two differ only for keys that actually contain escapes. See [Types.md](Types.md#which-key-to-use-for-a-lookup).

```csharp
// Round-trip a key that contains an escape
using var obj = doc.GetObject();
var keys = new List<string>();
foreach (var prop in obj)
{
    keys.Add(prop.EscapedName);
    prop.Value.Dispose();
}

doc.Rewind();
using var again = doc.GetField(keys[0]);
```

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

// Optional field lookup — a miss leaves the cursor where it started
obj.Reset();
using var hostAgain = obj.FindField("host");
_ = hostAgain.GetString();
if (obj.TryFindField("timeout", out var timeout))
{
    using (timeout) Console.WriteLine(timeout!.GetInt64());
}
else
{
    using var tls = obj.FindField("tls"); // still found after the miss
    Console.WriteLine(tls.GetBool());
}
```

`TryFindField` only restores the cursor when the key is absent. If found, it advances just like `FindField`; consume the returned value before reading a later sibling. Each found `JsonValue` must be disposed.

The native position snapshot is created and consumed inside this call. No checkpoint token is exposed, so it cannot be applied to a different object or reused after `Reset()`. Errors other than a missing key propagate without restoring the cursor.

> **`GetField` vs `FindField`**: use `GetField` (or the indexer) for order-insensitive access. Use `FindField` and `TryFindField` when reading fields in document order. A missing `TryFindField` does not rewind or rescan fields consumed before the call.

---

← [API Reference](API.md)
