# API Reference

Complete reference for the `SimdJson.Net` public API.

## Classes & types

| Type | Description |
|------|-------------|
| [SimdJsonParser](SimdJsonParser.md) | Main entry point — parse JSON, static utilities |
| [JsonDocument](JsonDocument.md) | Root result of a parse call; holds the document iterator |
| [JsonValue](JsonValue.md) | A single JSON value — scalar, array, or object |
| [JsonArray](JsonArray.md) | A JSON array with iteration and index access |
| [JsonObject](JsonObject.md) | A JSON object with field lookup and iteration |
| [NdjsonParser](NdjsonParser.md) | Sequential and parallel NDJSON (newline-delimited JSON) parser |
| [Numbers](Numbers.md) | `JsonNumberType` enum and `JsonNumber` struct |
| [Types & Errors](Types.md) | `JsonValueKind`, `JsonProperty`, `SimdJsonException` error codes |

## Key concepts

- **Parsing**: call `SimdJsonParser.Shared.Parse(json)` or `new SimdJsonParser().ParseAsync(stream)`.
- **Forward-only iterator**: simdjson On-Demand processes JSON in a single pass. Fields must be consumed in order, or use `GetField`/`this[key]` for order-insensitive lookup.
- **Dispose everything**: `JsonDocument`, `JsonValue`, `JsonArray`, and `JsonObject` all hold native handles. Always wrap in `using`.
- **One document per parser**: a parser instance can only hold one live document at a time.

For patterns, pitfalls, and detailed rules see [Design Notes](DesignNotes.md).

## Quick lookup

### Parsing

```csharp
using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
using var doc = SimdJsonParser.Shared.Parse(utf8Span);
using var doc = await parser.ParseAsync(stream, ct);
```

### Scalar access

```csharp
using var v = doc.GetField("name");
v.GetString();     v.GetStringSpan();
v.GetInt64();      v.GetUInt64();    v.GetDouble();
v.GetBool();       v.IsNull();
```

### Arrays

```csharp
using var arr = doc.GetArray();          // or val.GetArray()
foreach (var item in arr) { ...; item.Dispose(); }
using var el = arr.At(0);               // by index
Console.WriteLine(arr.Count);
Console.WriteLine(arr.IsEmpty());
```

### Objects

```csharp
using var obj = doc.GetObject();         // or val.GetObject()
using var val = obj.GetField("key");     // order-insensitive
obj.TryGetField("opt", out var opt);
foreach (var prop in obj) { ...; prop.Value.Dispose(); }
```

### Pointers & paths

```csharp
using var v = doc.AtPointer("/store/items/0/name"); // RFC 6901
using var v = doc.AtPath("$.store.items[0].name");  // JSONPath
doc.TryAtPointer("/optional", out var v);
```

### Numbers

```csharp
JsonNumber n = val.GetNumber();
// n.NumberType, n.AsDouble(), n.AsInt64(), n.AsUInt64()
```

### Utilities

```csharp
SimdJsonParser.GetVersion();           // "4.6.3"
SimdJsonParser.Minify(json);
SimdJsonParser.ValidateUtf8(bytes);
```

### Raw JSON string

```csharp
using var val = doc.GetField("key");
val.GetRawJsonString();      // escaped bytes as string, no surrounding quotes
val.GetRawJsonStringSpan();  // same, zero-allocation ReadOnlySpan<byte>
// "hello\nworld" in JSON → GetRawJsonString() returns @"hello\nworld" (backslash-n)
// GetString() returns "hello\nworld" (real newline)
```

### GetRawJsonSpan on containers

```csharp
using var arr = val.GetArray();
ReadOnlySpan<byte> raw = arr.GetRawJsonSpan();   // e.g. [1,2,3]

using var obj = val.GetObject();
ReadOnlySpan<byte> raw = obj.GetRawJsonSpan();   // e.g. {"a":1}

ReadOnlySpan<byte> raw = doc.GetRawJsonSpan();   // whole document
```

### Counting elements and fields

```csharp
// On a document
int n = doc.CountElements();   // root array: count elements (full scan)
int n = doc.CountFields();     // root object: count fields  (full scan)

// On a value
int n = val.CountElements();   // value must be an array
int n = val.CountFields();     // value must be an object
```

> **Note:** counting requires a full forward scan. After calling `CountElements`/`CountFields` the iterator is exhausted. Call `Rewind()` on the document to reset.

### Document scalar getters

```csharp
// Use when the root of the document is a bare scalar
string  s = doc.GetString();
bool    b = doc.GetBool();
bool    n = doc.IsNull();
double  d = doc.GetDouble();
long    i = doc.GetInt64();
ulong   u = doc.GetUInt64();

// Numbers stored as JSON strings
long   i = doc.GetInt64InString();
ulong  u = doc.GetUInt64InString();
double d = doc.GetDoubleInString();

// Direct element access on a root array
using var val = doc.At(2);   // 0-based index
```

### 32-bit integers

```csharp
int  i = val.GetInt32();             // throws if not int32 or overflow
uint u = val.GetUInt32();            // throws if not uint32 or overflow
val.TryGetInt32(out int  v);         // returns false on type mismatch or overflow
val.TryGetUInt32(out uint v);        // returns false on type mismatch or overflow
```

### Wildcard path iteration

```csharp
// Visit every element of an array
doc.ForEachAtPath("$[*]", v => Console.WriteLine(v.GetInt64()));

// Extract one field from each object in an array
doc.ForEachAtPath("$.items[*].name", v => Console.WriteLine(v.GetString()));

// Visit all field values of an object
doc.ForEachAtPath("$.*", v => Console.WriteLine(v.GetDouble()));

// Start from a value instead of the document root
using var arrVal = doc.GetField("data");
arrVal.ForEachAtPath("$[*]", v => results.Add(v.GetString()));

// Start from an array or object handle
using var arr = val.GetArray();
arr.ForEachAtPath("$[*]", v => results.Add(v.GetInt64()));

using var obj = val.GetObject();
obj.ForEachAtPath("$.*", v => results.Add(v.GetString()));
```

> The `JsonValue` passed to the callback is **borrowed**: valid only during the callback invocation. Do not dispose or store it.

### NDJSON (newline-delimited JSON)

```csharp
// Sequential — results arrive in file order
await foreach (var r in NdjsonParser.ParseAsync(stream, doc =>
{
    using var v = doc.GetField("name");
    return v.GetString();
}))
    Console.WriteLine(r);

// Parallel — results arrive in completion order; all CPU cores used
await foreach (var r in NdjsonParser.ParseParallelAsync(stream, doc =>
{
    using var v = doc.GetField("id");
    return v.GetInt64();
}))
    Console.WriteLine(r);

// Side-effect — parallel, no result projection
await NdjsonParser.ForEachAsync(stream, doc =>
{
    using var v = doc.GetField("score");
    Interlocked.Add(ref total, (long)v.GetDouble());
});

// Options
var opts = new NdjsonParserOptions
{
    MaxDegreeOfParallelism = 4,  // worker count (default: Environment.ProcessorCount)
    ChannelCapacity        = 64, // 0 = auto (DOP×4)
    ReadBufferSize         = 65_536,
    InitialLineBufferSize  = 4_096,
    SkipMalformedLines     = true,  // swallow bad JSON silently
    SkipEmptyLines         = true,
    LeaveOpen              = false, // dispose stream when done
};
```

See [NdjsonParser](NdjsonParser.md) for the full reference.
