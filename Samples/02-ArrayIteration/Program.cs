// 02 – Array Iteration
// Demonstrates foreach, At(index), ElementAt, Count, IsEmpty, and nested arrays.

using SimdJson;

Console.WriteLine("=== 02 – Array Iteration ===\n");

// ── 1. Simple array with foreach ───────────────────────────────────────────
using var doc = SimdJsonParser.Shared.Parse("""[10, 20, 30, 40, 50]""");
using var arr = doc.GetArray();

Console.WriteLine($"Count   : {arr.Count}");
Console.WriteLine($"IsEmpty : {arr.IsEmpty()}");
Console.WriteLine("Elements:");
foreach (var item in arr)
{
    Console.WriteLine($"  {item.GetInt64()}");
    item.Dispose();
}

// ── 2. Array of strings ────────────────────────────────────────────────────
Console.WriteLine();
using var doc2 = SimdJsonParser.Shared.Parse("""["apple","banana","cherry"]""");
using var fruits = doc2.GetArray();
foreach (var fruit in fruits)
{
    Console.WriteLine($"  {fruit.GetString()}");
    fruit.Dispose();
}

// ── 3. Access by index with At() ───────────────────────────────────────────
Console.WriteLine();
using var doc3 = SimdJsonParser.Shared.Parse("""["zero","one","two","three"]""");
using var words = doc3.GetArray();
using var second = words.At(1);
Console.WriteLine($"At(1)      : {second.GetString()}");   // one
words.Reset();
using var third  = words.ElementAt(2);
Console.WriteLine($"ElementAt(2): {third.GetString()}");  // two

// ── 4. Array of objects ────────────────────────────────────────────────────
Console.WriteLine();
const string peopleJson = """
[
  {"name": "Alice", "age": 30},
  {"name": "Bob",   "age": 25},
  {"name": "Carol", "age": 35}
]
""";
using var doc4   = SimdJsonParser.Shared.Parse(peopleJson);
using var people = doc4.GetArray();
foreach (var person in people)
{
    using var pName = person.GetField("name");
    using var pAge  = person.GetField("age");
    Console.WriteLine($"  {pName.GetString()}, age {pAge.GetInt64()}");
    person.Dispose();
}

// ── 5. Nested array ────────────────────────────────────────────────────────
Console.WriteLine();
using var doc5   = SimdJsonParser.Shared.Parse("""{"matrix":[[1,2],[3,4],[5,6]]}""");
using var matrix = doc5.GetField("matrix").GetArray();
foreach (var row in matrix)
{
    using var rowArr = row.GetArray();
    var cells = new List<long>();
    foreach (var cell in rowArr)
    {
        cells.Add(cell.GetInt64());
        cell.Dispose();
    }
    Console.WriteLine($"  [{string.Join(", ", cells)}]");
    row.Dispose();
}

// ── 6. Empty array ─────────────────────────────────────────────────────────
Console.WriteLine();
using var doc6  = SimdJsonParser.Shared.Parse("""{"items":[]}""");
using var empty = doc6.GetField("items").GetArray();
Console.WriteLine($"Empty array — IsEmpty: {empty.IsEmpty()}, Count: {empty.Count}");
