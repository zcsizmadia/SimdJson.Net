// 06 – Stream Parsing
// Demonstrates ParseAsync(Stream) and ParseAsync(string) — useful when the
// JSON arrives from a file, HTTP response, or any other async source.

using System.Text;
using SimdJson;

Console.WriteLine("=== 06 – Stream Parsing ===\n");

// ── 1. Parse from a MemoryStream (stands in for any Stream) ───────────────
Console.WriteLine("── ParseAsync(Stream) ──");

const string json = """
{
  "service": "inventory",
  "version": 2,
  "items": [
    {"sku": "A001", "qty": 100},
    {"sku": "B002", "qty": 0},
    {"sku": "C003", "qty": 50}
  ]
}
""";

await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
using var parser = new SimdJsonParser();
using var doc    = await parser.ParseAsync(stream);

using var service = doc.GetField("service");
using var version = doc.GetField("version");
Console.WriteLine($"service : {service.GetString()}");
Console.WriteLine($"version : {version.GetInt64()}");

using var items = doc.GetField("items").GetArray();
foreach (var item in items)
{
    using var sku = item.GetField("sku");
    using var qty = item.GetField("qty");
    Console.WriteLine($"  {sku.GetString()} — qty {qty.GetInt64()}");
    item.Dispose();
}

// ── 2. Parse from a FileStream ────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── ParseAsync from a temp file ──");

var tmpFile = Path.GetTempFileName();
try
{
    await File.WriteAllTextAsync(tmpFile, """{"status":"ok","count":42}""");

    await using var fs  = File.OpenRead(tmpFile);
    using var fileDoc   = await parser.ParseAsync(fs);
    using var status    = fileDoc.GetField("status");
    using var count     = fileDoc.GetField("count");
    Console.WriteLine($"status : {status.GetString()}");
    Console.WriteLine($"count  : {count.GetInt64()}");
}
finally
{
    File.Delete(tmpFile);
}

// ── 3. ParseAsync(string) — offloads transcoding to thread pool ───────────
Console.WriteLine();
Console.WriteLine("── ParseAsync(string) ──");

using var strDoc = await parser.ParseAsync("""{"hello":"world"}""");
using var hello  = strDoc.GetField("hello");
Console.WriteLine($"hello : {hello.GetString()}");

// ── 4. Cancellation ───────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Cancellation ──");

using var cts = new CancellationTokenSource();
// Cancel immediately to demonstrate the path
cts.Cancel();

try
{
    await using var cancelStream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
    using var _ = await parser.ParseAsync(cancelStream, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("ParseAsync cancelled as expected.");
}
