// 11 – Wildcard Path Iteration & Raw JSON Strings
// Demonstrates:
//   - doc.ForEachAtPath(path, callback)  — wildcard JSONPath iteration
//   - val.ForEachAtPath(path, callback)  — wildcard iteration from a value
//   - val.GetRawJsonString()             — escaped bytes, no unescaping
//   - val.GetRawJsonStringSpan()         — zero-allocation variant

using System.Text;
using SimdJson;

Console.WriteLine("=== 11 – Wildcard Path Iteration & Raw JSON Strings ===\n");

// ─── 1. GetRawJsonString vs GetString ─────────────────────────────────────
// GetString()        → fully decoded UTF-8 (backslash escapes resolved)
// GetRawJsonString() → raw bytes as they appear in the source JSON,
//                      minus the surrounding quote characters.
//
// Useful when you need to round-trip JSON strings verbatim,
// compare raw representations, or pass the bytes directly to another parser.

Console.WriteLine("── 1. GetRawJsonString vs GetString ──");
using var doc1 = SimdJsonParser.Shared.Parse("""{"greeting":"hello\nworld","path":"C:\\\\Users\\\\Alice"}""");

using var greet = doc1.GetField("greeting");
Console.WriteLine($"GetString        : {greet.GetString()}");        // hello
                                                                      // world
Console.WriteLine($"GetRawJsonString : {greet.GetRawJsonString()}");  // hello\nworld (2 chars: \n)

doc1.Rewind();
using var path = doc1.GetField("path");
Console.WriteLine($"GetString        : {path.GetString()}");          // C:\Users\Alice
Console.WriteLine($"GetRawJsonString : {path.GetRawJsonString()}");   // C:\\\\Users\\\\Alice (four backslashes → two in raw JSON)

// ─── 2. GetRawJsonStringSpan — zero allocation ────────────────────────────
Console.WriteLine("\n── 2. GetRawJsonStringSpan (no string allocation) ──");
using var doc2 = SimdJsonParser.Shared.Parse("""{"tag":"simdjson"}""");
using var tag = doc2.GetField("tag");

ReadOnlySpan<byte> rawSpan = tag.GetRawJsonStringSpan();
// Compare without allocating a string
bool match = rawSpan.SequenceEqual("simdjson"u8);
Console.WriteLine($"rawSpan matches 'simdjson': {match}");           // True
Console.WriteLine($"byte count                : {rawSpan.Length}");  // 8

// ─── 3. ForEachAtPath — array wildcard ────────────────────────────────────
// "$.prices[*]" visits every element of the "prices" array.
Console.WriteLine("\n── 3. ForEachAtPath — array wildcard ──");
using var doc3 = SimdJsonParser.Shared.Parse("""{"prices":[1.99,2.49,3.99,0.99]}""");

var prices = new List<double>();
doc3.ForEachAtPath("$.prices[*]", v => prices.Add(v.GetDouble()));
Console.WriteLine($"prices : [{string.Join(", ", prices)}]");  // [1.99, 2.49, 3.99, 0.99]
Console.WriteLine($"min    : {prices.Min()}");
Console.WriteLine($"max    : {prices.Max()}");

// ─── 4. ForEachAtPath — nested wildcard (extract one field from array of objects) ──
Console.WriteLine("\n── 4. ForEachAtPath — nested wildcard ──");
const string catalogJson = """
{
  "catalog": [
    { "id": 1, "name": "Widget A", "stock": 42 },
    { "id": 2, "name": "Widget B", "stock":  7 },
    { "id": 3, "name": "Widget C", "stock": 120 }
  ]
}
""";

using var doc4 = SimdJsonParser.Shared.Parse(catalogJson);

Console.WriteLine("Product names:");
doc4.ForEachAtPath("$.catalog[*].name", v =>
    Console.WriteLine($"  {v.GetString()}"));

doc4.Rewind();
Console.WriteLine("Total stock:");
int totalStock = 0;
doc4.ForEachAtPath("$.catalog[*].stock", v => totalStock += (int)v.GetInt64());
Console.WriteLine($"  {totalStock}");

// ─── 5. ForEachAtPath — object wildcard ───────────────────────────────────
// "$.*" visits every field value in the root object.
Console.WriteLine("\n── 5. ForEachAtPath — object wildcard ──");
using var doc5 = SimdJsonParser.Shared.Parse("""{"a":10,"b":20,"c":30}""");

var fieldValues = new List<long>();
doc5.ForEachAtPath("$.*", v => fieldValues.Add(v.GetInt64()));
Console.WriteLine($"All field values : [{string.Join(", ", fieldValues)}]"); // [10, 20, 30]

// ─── 6. ForEachAtPath starting from a value ───────────────────────────────
Console.WriteLine("\n── 6. ForEachAtPath from a value ──");
const string eventsJson = """
{
  "batch": {
    "events": [
      {"type":"click","target":"button"},
      {"type":"hover","target":"link"},
      {"type":"click","target":"icon"}
    ]
  }
}
""";

using var doc6 = SimdJsonParser.Shared.Parse(eventsJson);
using var batchVal = doc6["batch"];

Console.WriteLine("Event types:");
batchVal.ForEachAtPath("$.events[*].type", v =>
    Console.WriteLine($"  {v.GetString()}"));

Console.WriteLine("\nDone.");
