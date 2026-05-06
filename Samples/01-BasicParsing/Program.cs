// 01 – Basic Parsing
// Demonstrates the most common entry points: Parse(string), Parse(ReadOnlySpan<byte>),
// the thread-local Shared parser, and reading scalar fields from a JSON object.

using SimdJson;

Console.WriteLine("=== 01 – Basic Parsing ===\n");

// ── 1. Parse a string ──────────────────────────────────────────────────────
const string json = """{"name":"Alice","age":30,"active":true,"score":9.5,"notes":null}""";

using var doc = SimdJsonParser.Shared.Parse(json);

using var name   = doc.GetField("name");
using var age    = doc.GetField("age");
using var active = doc.GetField("active");
using var score  = doc.GetField("score");
using var notes  = doc.GetField("notes");

Console.WriteLine($"name   : {name.GetString()}");       // Alice
Console.WriteLine($"age    : {age.GetInt64()}");         // 30
Console.WriteLine($"active : {active.GetBool()}");       // True
Console.WriteLine($"score  : {score.GetDouble()}");      // 9.5
Console.WriteLine($"notes  : {notes.IsNull()}");         // True (it is null)

// ── 2. Parse UTF-8 bytes directly (zero-copy, no string allocation) ────────
Console.WriteLine();
ReadOnlySpan<byte> utf8 = """{"city":"Budapest","country":"Hungary"}"""u8;
using var doc2 = SimdJsonParser.Shared.Parse(utf8);
using var city    = doc2.GetField("city");
using var country = doc2.GetField("country");
Console.WriteLine($"city    : {city.GetString()}");
Console.WriteLine($"country : {country.GetString()}");

// ── 3. Use a dedicated parser (not thread-local) ───────────────────────────
Console.WriteLine();
using var parser = new SimdJsonParser();
using var doc3   = parser.Parse("""{"version":"4.6.3"}""");
using var ver    = doc3.GetField("version");
Console.WriteLine($"version : {ver.GetString()}");

// ── 4. Check the simdjson library version ─────────────────────────────────
Console.WriteLine($"\nsimdjson version: {SimdJsonParser.GetVersion()}");

// ── 5. Indexer syntax (alias for GetField) ─────────────────────────────────
Console.WriteLine();
using var doc4  = SimdJsonParser.Shared.Parse(json);
using var name2 = doc4["name"];
Console.WriteLine($"indexer: {name2.GetString()}");
