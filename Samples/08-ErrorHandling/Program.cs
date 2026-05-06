// 08 – Error Handling
// Demonstrates SimdJsonException error codes, TryXxx non-throwing API,
// ParseAllowIncompleteJson, and how to distinguish error cases.

using SimdJson;

Console.WriteLine("=== 08 – Error Handling ===\n");

// ── Helper ────────────────────────────────────────────────────────────────
static void Try(string label, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  {label}: (no exception)");
    }
    catch (SimdJsonException ex)
    {
        Console.WriteLine($"  {label}: SimdJsonException code={ex.ErrorCode} — {ex.Message}");
    }
}

// ── 1. Invalid JSON ────────────────────────────────────────────────────────
// On-Demand is lazy: the error only surfaces when data is actually accessed.
Console.WriteLine("── Parse errors ──");
Try("invalid json",   () => { using var d = SimdJsonParser.Shared.Parse("{bad"); using var o = d.GetObject(); foreach (var p in o) p.Value.Dispose(); });
Try("trailing comma", () => { using var d = SimdJsonParser.Shared.Parse("""{{"a":1,}}"""); using var o = d.GetObject(); foreach (var p in o) p.Value.Dispose(); });

// ── 2. Wrong type ──────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Wrong-type errors ──");
using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice","count":5}""");
Try("GetInt64 on string", () =>
{
    using var v = doc.GetField("name");
    _ = v.GetInt64();   // "Alice" is not a number
});
doc.Rewind();
Try("GetString on number", () =>
{
    using var v = doc.GetField("count");
    _ = v.GetString();  // 5 is not a string
});

// ── 3. Missing field ───────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Missing field ──");
doc.Rewind();
Try("missing field", () =>
{
    using var v = doc.GetField("missing");
});

// ── 4. TryXxx — non-throwing alternatives ─────────────────────────────────
Console.WriteLine();
Console.WriteLine("── TryGetField / TryAtPointer ──");
doc.Rewind();
if (doc.TryGetField("name", out var nameVal))
{
    Console.WriteLine($"  name found : {nameVal!.GetString()}");
    nameVal.Dispose();
}
doc.Rewind();
if (!doc.TryGetField("missing", out var missVal))
{
    missVal?.Dispose();
    Console.WriteLine("  'missing' not found — no exception");
}

doc.Rewind();
if (!doc.TryAtPointer("/does/not/exist", out var ptrVal))
{
    ptrVal?.Dispose();
    Console.WriteLine("  pointer miss — no exception");
}

// ── 5. Out-of-bounds array access ─────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Array out of bounds ──");
using var arrDoc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
using var arr    = arrDoc.GetArray();
Try("At(10)",    () => { using var v = arr.At(10); });
Try("ElementAt(10)", () => { using var v = arr.ElementAt(10); });

// ── 6. Accessing a disposed value ─────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── ObjectDisposedException ──");
var disposedDoc = SimdJsonParser.Shared.Parse("""{"x":1}""");
disposedDoc.Dispose();
try
{
    _ = disposedDoc.ValueKind;
}
catch (ObjectDisposedException ex)
{
    Console.WriteLine($"  ObjectDisposedException: {ex.ObjectName}");
}

// ── 7. ParseAllowIncompleteJson ────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── ParseAllowIncompleteJson ──");
// Normally truncated JSON would throw; this API is lenient.
const string truncated = """{"name":"Alice","age":""";
try
{
    using var partial = SimdJsonParser.Shared.ParseAllowIncompleteJson(truncated);
    // Only fields that were fully parsed before the cut-off are accessible.
    using var pName = partial.GetField("name");
    Console.WriteLine($"  partial name : {pName.GetString()}");
}
catch (SimdJsonException ex)
{
    // Some truncations are still unrecoverable
    Console.WriteLine($"  incomplete parse error: {ex.Message}");
}

// Normal parse of same string should throw
// Normal parse: lazy too — must access fields to trigger the error.
Try("normal Parse of truncated", () =>
{
    using var d    = SimdJsonParser.Shared.Parse(truncated);
    using var name = d.GetField("name");
    _ = name.GetString();          // "name" is complete — no error yet
    using var age = d.GetField("age"); // "age" value is missing — should throw
    _ = age.GetInt64();
});
