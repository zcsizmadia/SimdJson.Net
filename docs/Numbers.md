# Numbers

## `JsonNumberType`

Returned by `JsonValue.GetNumberType()` to identify the sub-type of a JSON number before reading it with a typed getter.

| Value | Description | Recommended getter |
|-------|-------------|--------------------|
| `FloatingPoint` | A `double` (e.g. `3.14`, `1e10`) | `GetDouble()` |
| `SignedInteger` | A `long` in the range [−2⁶³, 2⁶³−1] (e.g. `−42`, `0`) | `GetInt64()` |
| `UnsignedInteger` | A `ulong` ≥ 2⁶³ (e.g. `10000000000000000000`) | `GetUInt64()` |
| `BigInteger` | An integer outside the 64-bit range, including positive values from `2^64` through `10^20 - 1` with simdjson 5 — read via `GetRawJsonToken()` | — |

Big integers are classified and preserved as JSON number tokens; the library does not convert them to `System.Numerics.BigInteger`. Use the raw-token accessor to retain all decimal digits. For a root number, use `JsonDocument.GetRawJsonToken()` or `GetRawJsonTokenSpan()`.

Use `GetFloat()` when single precision is sufficient. It parses the JSON number directly to
`float`, avoiding an intermediate `double` rounding step. `GetDouble()` continues to return a
binary64 value.

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
| `AsDouble()` | Value as `double` — works for `FloatingPoint`, `SignedInteger`, and `UnsignedInteger`; throws `InvalidOperationException` for `BigInteger` |
| `AsInt64()` | Value as `long` — meaningful only for `SignedInteger` |
| `AsUInt64()` | Value as `ulong` — meaningful only for `UnsignedInteger` |
| `ToString()` | Decimal representation for supported numeric values; returns `big_integer` for `BigInteger` because `JsonNumber` does not store its digits |

> For a `BigInteger`, `GetNumber()` succeeds and reports the type, but carries no numeric value —
> read the digits with `JsonValue.GetRawJsonToken()` (or `JsonDocument.GetRawJsonToken()` for a root number).

### Example

```csharp
using var val  = doc.GetField("price");
JsonNumber num = val.GetNumber();

Console.WriteLine(num.NumberType); // FloatingPoint
Console.WriteLine(num.AsDouble()); // 9.99
Console.WriteLine(num.ToString()); // 9.99
```

For a root big integer, preserve the exact digits from the document:

```csharp
using var doc = SimdJsonParser.Shared.Parse("18446744073709551616");
if (doc.GetNumberType() == JsonNumberType.BigInteger)
{
    string exactDigits = doc.GetRawJsonToken();
}
```

### `IsNegative` / `IsInteger`

Quick predicates available directly on `JsonValue`, useful when you only need a boolean check without reading the full value:

```csharp
using var v = doc.GetField("score");
Console.WriteLine(v.IsNegative()); // true for -3.14
Console.WriteLine(v.IsInteger());  // false for 3.14, true for 3
```


## NaN and Infinity

Strict JSON has no way to write `NaN` or `Infinity`, but many producers emit them anyway. They are rejected by default. Set `AllowNanInfinity` on the parser to accept them:

```csharp
using var parser = new SimdJsonParser { AllowNanInfinity = true };
using var doc = parser.Parse("""{"a":NaN,"b":-Infinity,"c":"Infinity"}""");

using var a = doc.GetField("a");
Console.WriteLine(double.IsNaN(a.GetDouble()));                      // true
using var b = doc.GetField("b");
Console.WriteLine(double.IsNegativeInfinity(b.GetDouble()));         // true
using var c = doc.GetField("c");
Console.WriteLine(double.IsPositiveInfinity(c.GetDoubleInString())); // true
```

| Detail | Behaviour |
|--------|-----------|
| Accepted tokens | `NaN`, `Infinity`, `-Infinity`, and the short forms `nan`, `inf`, `-inf`. Matching is case-insensitive |
| Rejected tokens | `+Infinity`, and anything that merely starts like one (`NaNx`, `Infinit`) |
| Members affected | `GetDouble`, `GetFloat`, `GetNumber`, `GetDoubleInString` (and their `Try` forms) on `JsonValue` and `JsonDocument` |
| Default | Off. The same tokens throw `SimdJsonException` (`-2`, or `-6` from `GetNumber`), exactly as before |
| Out-of-range numbers | `1e999` still throws; it never becomes infinity |
| Scope | Captured when a document is created. Changing the flag later does not change existing documents |
| Streams | `NdjsonParserOptions.AllowNanInfinity` applies it to every stream and NDJSON parsing method |

simdjson recognises these tokens only when a number is read. `ValueKind` reports `Unknown` for a bare `NaN` or `Infinity` (and `Null` for `nan`), and `GetNumberType` throws for them. Read such values with `GetDouble` or `TryGetDouble` directly instead of checking the kind first.

`SimdJsonParser.Shared` is one instance per thread, so setting the flag on it affects only the calling thread. Use a dedicated parser instance instead.

---

← [API Reference](API.md)
