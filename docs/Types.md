# Types & Errors

## `JsonValueKind`

Returned by `JsonDocument.ValueKind` and `JsonValue.ValueKind`.

| Value | JSON type |
|-------|-----------|
| `Array` | `[…]` |
| `Object` | `{…}` |
| `Number` | Numeric literal |
| `String` | `"…"` |
| `Boolean` | `true` or `false` |
| `Null` | `null` |
| `Unknown` | Not yet determined (iterator not yet advanced) |

### Example

```csharp
using var obj = doc.GetObject();
foreach (var prop in obj)
{
    string display = prop.Value.ValueKind switch
    {
        JsonValueKind.String  => prop.Value.GetString(),
        JsonValueKind.Number  => prop.Value.GetDouble().ToString(),
        JsonValueKind.Boolean => prop.Value.GetBool().ToString(),
        JsonValueKind.Null    => "null",
        _                     => prop.Value.GetRawJson(),
    };
    Console.WriteLine($"{prop.Name} = {display}");
    prop.Value.Dispose();
}
```

---

## `JsonProperty`

Yielded from `foreach` over a [`JsonObject`](JsonObject.md).

| Member | Description |
|--------|-------------|
| `Name` | Unescaped field key as a `string` |
| `Value` | Field value as a [`JsonValue`](JsonValue.md) — **must be disposed** |

> Dispose `prop.Value` before each iteration step or the native iterator may become invalid.

---

## `SimdJsonException`

Thrown on any native bridge error. `Message` contains a human-readable description; `ErrorCode` holds the raw integer code.

| `ErrorCode` | Meaning |
|-------------|---------|
| `-1` | Parser capacity exceeded |
| `-2` | Incorrect JSON value type |
| `-3` | No such field |
| `-4` | Index out of bounds |
| `-5` | Null pointer passed to bridge |
| `-6` | JSON parse error (malformed JSON) |
| `-7` | Iteration error (forward-only constraint violated) |
| `-8` | Invalid JSON Pointer syntax |
| `-9` | Scalar document used as a container |
| `-99` | Unknown native error |

### Catching errors

```csharp
try
{
    using var val = doc.GetField("missing");
}
catch (SimdJsonException ex) when (ex.ErrorCode == -3)
{
    Console.WriteLine("Field not found");
}
```

### Non-throwing alternatives

Most lookup methods have `TryXxx` counterparts that return `false` (or `false` + `out null`) instead of throwing:

```csharp
if (doc.TryGetField("optional", out var v))
{
    using (v) Console.WriteLine(v!.GetString());
}

if (doc.TryAtPointer("/config/timeout", out var t))
{
    using (t) Console.WriteLine(t!.GetInt64());
}
```

---

← [API Reference](API.md)
