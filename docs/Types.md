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
| `-10` | Number out of range (does not fit in 64 bits, or in the requested type) |
| `-11` | Native memory allocation failed |
| `-12` | Maximum JSON nesting depth exceeded |
| `-13` | Unexpected trailing content after the JSON value |
| `-99` | Unknown native error carrying no recoverable detail |
| `-1001` and below | A simdjson error with no dedicated code above; see below |

### simdjson errors without a dedicated code

simdjson defines more error conditions than the table above. Rather than collapsing all of them to `-99`, the bridge encodes the original error as `-1000` minus the simdjson code, and `Message` carries simdjson's own text plus the underlying number.

```
Unsupported architecture (simdjson error 21).
```

These codes are stable for a given simdjson version but are not part of this package's API surface, because upstream may renumber them. Match on the documented codes above; treat the encoded range as diagnostic detail for logs and bug reports.

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

#### What `false` means, and what it does not

A `TryXxx` method answers one question. It does not swallow unrelated failures, so a `false` result is always meaningful rather than a catch-all.

| Method group | Returns `false` for | Everything else |
|--------------|---------------------|-----------------|
| `TryGetField`, `TryFindFieldUnordered`, `TryAtPointer`, `TryAtPath`, `ContainsKey` | `-3` no such field, `-4` index out of bounds, `-8` malformed pointer | throws |
| `TryGetString`, `TryGetInt64`, `TryGetArray` and the other typed getters | `-2` wrong type, `-9` scalar document as container, `-10` number out of range | throws |
| `TryGetInt64InString` and the other number-in-string getters | the above, plus `-6` when the string's contents are not a number | throws |

So a malformed document, a capacity or depth limit, or asking an array for a field by name all raise `SimdJsonException` even through a `Try` call. Those are not absences; reporting them as one would hide the real problem at the point where it is cheapest to find.

Out-of-order iteration, `-7`, would also propagate, but note that simdjson only detects it in builds with development checks enabled, which is not the case for the binaries this package ships.

---

← [API Reference](API.md)
