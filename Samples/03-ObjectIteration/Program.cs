// 03 – Object Iteration
// Demonstrates foreach over JsonProperty, GetField vs FindField,
// FindFieldUnordered, TryGetField, IsEmpty, Count, ContainsKey,
// and iterating nested objects.

using SimdJson;

Console.WriteLine("=== 03 – Object Iteration ===\n");

// ── 1. Iterate all properties with foreach ─────────────────────────────────
const string configJson = """
{
  "host": "localhost",
  "port": 5432,
  "tls": true,
  "timeout": 30
}
""";
using var doc = SimdJsonParser.Shared.Parse(configJson);
using var obj = doc.GetObject();

Console.WriteLine($"Count  : {obj.Count}");
Console.WriteLine("Fields:");
foreach (var prop in obj)
{
    Console.WriteLine($"  {prop.Name} ({prop.Value.ValueKind})");
    prop.Value.Dispose();
}

// ── 2. GetField (order-insensitive) vs FindField (order-sensitive) ─────────
Console.WriteLine();
using var doc2 = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
using var valC = doc2.GetField("c");     // jump to any field
using var valA = doc2.GetField("a");     // order doesn't matter
Console.WriteLine($"GetField c={valC.GetInt64()}, a={valA.GetInt64()}");

using var doc3 = SimdJsonParser.Shared.Parse("""{"x":10,"y":20,"z":30}""");
using var valX = doc3.FindField("x");    // forward-only
using var valY = doc3.FindField("y");
Console.WriteLine($"FindField x={valX.GetInt64()}, y={valY.GetInt64()}");

// ── 3. TryGetField – non-throwing lookup ──────────────────────────────────
Console.WriteLine();
using var doc4 = SimdJsonParser.Shared.Parse("""{"name":"Bob"}""");
if (doc4.TryGetField("name", out var nameVal))
{
    Console.WriteLine($"TryGetField name: {nameVal!.GetString()}");
    nameVal.Dispose();
}
if (!doc4.TryGetField("missing", out var missing))
{
    missing?.Dispose();
    Console.WriteLine("TryGetField missing: not found (no exception)");
}

// ── 4. FindFieldUnordered ─────────────────────────────────────────────────
Console.WriteLine();
using var doc5 = SimdJsonParser.Shared.Parse("""{"z":99,"a":1,"m":42}""");
using var obj5 = doc5.GetObject();
using var valM = obj5.FindFieldUnordered("m");
Console.WriteLine($"FindFieldUnordered m={valM.GetInt64()}");

// ── 5. ContainsKey ────────────────────────────────────────────────────────
Console.WriteLine();
using var doc6 = SimdJsonParser.Shared.Parse("""{"exists":true}""");
using var obj6 = doc6.GetObject();
Console.WriteLine($"ContainsKey 'exists'  : {obj6.ContainsKey("exists")}");
Console.WriteLine($"ContainsKey 'missing' : {obj6.ContainsKey("missing")}");

// ── 6. Nested objects ─────────────────────────────────────────────────────
Console.WriteLine();
const string addressJson = """
{
  "person": {
    "name": "Carol",
    "address": {
      "street": "Main St",
      "city": "Springfield"
    }
  }
}
""";
using var doc7    = SimdJsonParser.Shared.Parse(addressJson);
using var person  = doc7.GetField("person").GetObject();
using var pName   = person.GetField("name");
using var address = person.GetField("address").GetObject();
using var street  = address.GetField("street");
using var city    = address.GetField("city");
Console.WriteLine($"{pName.GetString()} lives at {street.GetString()}, {city.GetString()}");

// ── 7. Dynamic/unknown schema – foreach + ValueKind switch ────────────────
Console.WriteLine();
const string mixedJson = """{"count":5,"label":"test","enabled":false,"ratio":0.75,"tag":null}""";
using var doc8 = SimdJsonParser.Shared.Parse(mixedJson);
using var obj8 = doc8.GetObject();
foreach (var prop in obj8)
{
    string display = prop.Value.ValueKind switch
    {
        JsonValueKind.String  => $"\"{prop.Value.GetString()}\"",
        JsonValueKind.Number  => prop.Value.GetDouble().ToString(),
        JsonValueKind.Boolean => prop.Value.GetBool().ToString(),
        JsonValueKind.Null    => "null",
        _                     => prop.Value.ValueKind.ToString()
    };
    Console.WriteLine($"  {prop.Name} = {display}");
    prop.Value.Dispose();
}
