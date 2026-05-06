# `JsonArray` *(IDisposable, IEnumerable\<JsonValue\>)*

Represents a JSON array obtained from a parsed document. Wraps a forward-only On-Demand array iterator.

> **Always dispose**: every `JsonArray` holds a native handle. Use `using var arr = ...`.

## Properties

| Member | Description |
|--------|-------------|
| `Count` | Number of elements — performs a full scan |
| `IsEmpty()` | `true` if the array has no elements — O(1), preferred over `Count == 0` |

## Iteration

```csharp
using var arr = doc.GetField("items").GetArray();
foreach (var item in arr)
{
    Console.WriteLine(item.GetString());
    item.Dispose(); // required — dispose each element
}
```

Every element yielded from `foreach` **must be disposed** before the next iteration step.

## Element access by index

| Member | Description |
|--------|-------------|
| `At(int)` | Element at zero-based index via the native `array.at()` call |
| `ElementAt(int)` | Element at zero-based index via forward iteration |

> `At()` and `ElementAt()` both advance the internal iterator. Call `Reset()` before reusing the array after either call.

## Pointer / path lookup

| Member | Description |
|--------|-------------|
| `AtPointer(string)` | JSON Pointer from this array (e.g. `"/0/name"`) |
| `AtPath(string)` | JSONPath from this array (e.g. `"$[0].name"`) |
| `TryAtPointer(string, out JsonValue?)` | Non-throwing `AtPointer` |
| `TryAtPath(string, out JsonValue?)` | Non-throwing `AtPath` |

## Iterator control & raw JSON

| Member | Description |
|--------|-------------|
| `Reset()` | Reset the array iterator to the beginning |
| `GetRawJson()` | Full raw JSON of the array as a `string` — consumes the iterator; call `Reset()` to re-iterate |
| `GetRawJsonSpan()` | Full raw JSON as a zero-allocation `ReadOnlySpan<byte>` — also consumes the iterator |

## Wildcard path iteration

| Member | Description |
|--------|-------------|
| `ForEachAtPath(string path, Action<JsonValue> callback)` | Invoke `callback` for each value matching a JSONPath wildcard expression (e.g. `"$[*]"`, `"$[*].name"`) starting from this array. The `JsonValue` passed to the callback is **borrowed** — valid only during the callback, must not be disposed or stored. |

## Examples

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"nums":[10,20,30],"words":["a","b","c"]}""");

// foreach
using var numsVal = doc.GetField("nums");
using var nums    = numsVal.GetArray();
foreach (var n in nums)
{
    Console.WriteLine(n.GetInt64());
    n.Dispose();
}

// Count
nums.Reset();
Console.WriteLine(nums.Count); // 3

// IsEmpty — faster than Count == 0
Console.WriteLine(nums.IsEmpty()); // False (after Reset)

// At / ElementAt — reset between calls
nums.Reset();
using var first = nums.At(0);
Console.WriteLine(first.GetInt64()); // 10

nums.Reset();
using var third = nums.ElementAt(2);
Console.WriteLine(third.GetInt64()); // 30

// Nested array
using var doc2 = SimdJsonParser.Shared.Parse("[[1,2],[3,4]]");
using var outer = doc2.GetArray();
foreach (var row in outer)
{
    using var inner = row.GetArray();
    foreach (var cell in inner)
    {
        Console.Write(cell.GetInt64() + " ");
        cell.Dispose();
    }
    Console.WriteLine();
    row.Dispose();
}
```

---

← [API Reference](API.md)
