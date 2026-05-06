# Numbers

## `JsonNumberType`

Returned by `JsonValue.GetNumberType()` to identify the sub-type of a JSON number before reading it with a typed getter.

| Value | Description | Recommended getter |
|-------|-------------|--------------------|
| `FloatingPoint` | A `double` (e.g. `3.14`, `1e10`) | `GetDouble()` |
| `SignedInteger` | A `long` in the range [−2⁶³, 2⁶³−1] (e.g. `−42`, `0`) | `GetInt64()` |
| `UnsignedInteger` | A `ulong` ≥ 2⁶³ (e.g. `10000000000000000000`) | `GetUInt64()` |
| `BigInteger` | An integer outside the 64-bit range — read via `GetRawJsonToken()` | — |

### Example

```csharp
using var val = doc.GetField("value");
switch (val.GetNumberType())
{
    case JsonNumberType.FloatingPoint:
        Console.WriteLine(val.GetDouble());
        break;
    case JsonNumberType.SignedInteger:
        Console.WriteLine(val.GetInt64());
        break;
    case JsonNumberType.UnsignedInteger:
        Console.WriteLine(val.GetUInt64());
        break;
    case JsonNumberType.BigInteger:
        Console.WriteLine(val.GetRawJsonToken()); // e.g. "99999999999999999999"
        break;
}
```

---

## `JsonNumber`

A tagged-union value type returned by `JsonValue.GetNumber()`. Retrieves both the type and the value in a single native call — preferred over calling `GetNumberType()` followed by a separate getter.

### Members

| Member | Description |
|--------|-------------|
| `NumberType` | `JsonNumberType` sub-type |
| `AsDouble()` | Value as `double` — works for `FloatingPoint`, `SignedInteger`, and `UnsignedInteger` |
| `AsInt64()` | Value as `long` — meaningful only for `SignedInteger` |
| `AsUInt64()` | Value as `ulong` — meaningful only for `UnsignedInteger` |
| `ToString()` | Decimal string representation |

### Example

```csharp
using var val  = doc.GetField("price");
JsonNumber num = val.GetNumber();

Console.WriteLine(num.NumberType); // FloatingPoint
Console.WriteLine(num.AsDouble()); // 9.99
Console.WriteLine(num.ToString()); // 9.99
```

### `IsNegative` / `IsInteger`

Quick predicates available directly on `JsonValue`, useful when you only need a boolean check without reading the full value:

```csharp
using var v = doc.GetField("score");
Console.WriteLine(v.IsNegative()); // true for -3.14
Console.WriteLine(v.IsInteger());  // false for 3.14, true for 3
```

---

← [API Reference](API.md)
