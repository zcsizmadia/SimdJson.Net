using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimdJson;

// ─── Sample data ─────────────────────────────────────────────────────────────
// 10 NDJSON records, plus one deliberately malformed line for demonstration.

const string ndjson = """
{"id":1,"name":"Alice","score":92.5}
{"id":2,"name":"Bob","score":88.0}
{"id":3,"name":"Carol","score":95.1}
{"id":4,"name":"Dan","score":77.3}
THIS IS NOT JSON
{"id":5,"name":"Eve","score":83.6}
{"id":6,"name":"Frank","score":91.0}
{"id":7,"name":"Grace","score":79.8}
{"id":8,"name":"Hank","score":88.4}
{"id":9,"name":"Iris","score":96.2}
{"id":10,"name":"Jack","score":84.7}
""";

// ─── Section 1: Sequential parsing with ParseAsync ───────────────────────────
Console.WriteLine("=== 1. Sequential (ParseAsync) — malformed lines skipped by default ===");

static (long id, string name, double score) SelectRecord(JsonDocument doc)
{
    using var idVal = doc.GetField("id");
    using var nameVal = doc.GetField("name");
    using var scoreVal = doc.GetField("score");
    return (idVal.GetInt64(), nameVal.GetString(), scoreVal.GetDouble());
}

var sequentialStream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
await foreach (var (id, name, score) in NdjsonParser.ParseAsync(sequentialStream, SelectRecord,
    new NdjsonParserOptions { LeaveOpen = false }))
{
    Console.WriteLine($"  id={id,-3} name={name,-6} score={score:F1}");
}

// ─── Section 2: Parallel parsing with ParseParallelAsync ─────────────────────
Console.WriteLine();
Console.WriteLine("=== 2. Parallel (ParseParallelAsync) — count + sum of scores ===");

const int bigLineCount = 1_000;
var bigBuilder = new StringBuilder(bigLineCount * 30);
for (int i = 1; i <= bigLineCount; i++)
    bigBuilder.AppendLine($"{{\"v\":{i}}}");

using var parallelStream = new MemoryStream(Encoding.UTF8.GetBytes(bigBuilder.ToString()));

var bag = new System.Collections.Concurrent.ConcurrentBag<long>();
await foreach (var v in NdjsonParser.ParseParallelAsync(
    parallelStream,
    doc => { using var f = doc.GetField("v"); return f.GetInt64(); },
    new NdjsonParserOptions { LeaveOpen = true }))
{
    bag.Add(v);
}

long expectedSum = (long)bigLineCount * (bigLineCount + 1) / 2;
Console.WriteLine($"  Lines parsed : {bag.Count}  (expected {bigLineCount})");
Console.WriteLine($"  Sum of v     : {bag.Sum()}  (expected {expectedSum})");
Console.WriteLine($"  Correct?     : {bag.Count == bigLineCount && bag.Sum() == expectedSum}");

// ─── Section 3: ForEachAsync with a side-effect ───────────────────────────────
Console.WriteLine();
Console.WriteLine("=== 3. ForEachAsync — parallel with side-effect counter ===");

int counter = 0;
using var sideEffectStream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
await NdjsonParser.ForEachAsync(sideEffectStream, doc =>
{
    using var idVal = doc.GetField("id");
    _ = idVal.GetInt64();           // just access a field
    Interlocked.Increment(ref counter);
}, new NdjsonParserOptions { LeaveOpen = false });

Console.WriteLine($"  Records processed: {counter}");

// ─── Section 4: Throw on malformed lines ─────────────────────────────────────
Console.WriteLine();
Console.WriteLine("=== 4. SkipMalformedLines = false — throws on bad JSON ===");

try
{
    using var strictStream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
    await foreach (var _ in NdjsonParser.ParseAsync(
        strictStream,
        doc => { using var idVal = doc.GetField("id"); return idVal.GetInt64(); },
        new NdjsonParserOptions { LeaveOpen = false, SkipMalformedLines = false }))
    { }
}
catch (SimdJsonException ex)
{
    Console.WriteLine($"  Caught expected SimdJsonException: {ex.Message}");
}

// ─── Section 5: CRLF + UTF-8 BOM + no trailing newline ───────────────────────
Console.WriteLine();
Console.WriteLine("=== 5. Edge cases: CRLF, UTF-8 BOM, no trailing newline ===");

// CRLF
using var crlfStream = new MemoryStream(
    Encoding.UTF8.GetBytes("{\"id\":1}\r\n{\"id\":2}\r\n{\"id\":3}\r\n"));
var crlfIds = new List<long>();
await foreach (var id in NdjsonParser.ParseAsync(crlfStream,
    doc => { using var v = doc.GetField("id"); return v.GetInt64(); }))
    crlfIds.Add(id);
Console.WriteLine($"  CRLF: {string.Join(", ", crlfIds)}");

// UTF-8 BOM
var bomBytes = new byte[] { 0xEF, 0xBB, 0xBF }
    .Concat(Encoding.UTF8.GetBytes("{\"id\":99}\n"))
    .ToArray();
using var bomStream = new MemoryStream(bomBytes);
var bomIds = new List<long>();
await foreach (var id in NdjsonParser.ParseAsync(bomStream,
    doc => { using var v = doc.GetField("id"); return v.GetInt64(); }))
    bomIds.Add(id);
Console.WriteLine($"  UTF-8 BOM stripped: id = {bomIds[0]}");

// No trailing newline
using var noNewlineStream = new MemoryStream(
    Encoding.UTF8.GetBytes("{\"id\":7}"));
var noNewlineIds = new List<long>();
await foreach (var id in NdjsonParser.ParseAsync(noNewlineStream,
    doc => { using var v = doc.GetField("id"); return v.GetInt64(); }))
    noNewlineIds.Add(id);
Console.WriteLine($"  No trailing newline: id = {noNewlineIds[0]}");

// ─── Section 6: Custom options ────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("=== 6. Custom options: small ReadBufferSize forces compaction ===");

using var smallBufStream = new MemoryStream(Encoding.UTF8.GetBytes(
    "{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n"));
var smallBufIds = new List<long>();
await foreach (var id in NdjsonParser.ParseAsync(smallBufStream,
    doc => { using var v = doc.GetField("id"); return v.GetInt64(); },
    new NdjsonParserOptions { ReadBufferSize = 16, LeaveOpen = false }))
    smallBufIds.Add(id);
Console.WriteLine($"  ReadBufferSize=16: {string.Join(", ", smallBufIds)}");

Console.WriteLine();
Console.WriteLine("Done.");
