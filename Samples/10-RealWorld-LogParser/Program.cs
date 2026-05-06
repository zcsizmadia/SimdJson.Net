// 10 – Real World: Structured Log Parser
// Simulates reading a newline-delimited JSON (NDJSON) log stream,
// filtering by log level, extracting fields, and aggregating stats.
// Demonstrates: parser reuse, forward-iteration discipline, GetRawJsonToken,
// TryGetField, and high-throughput line-by-line processing.

using SimdJson;

Console.WriteLine("=== 10 – Real World: Structured Log Parser ===\n");

// Newline-delimited JSON (one JSON object per line)
const string ndjson = """
{"ts":"2026-05-06T09:00:01Z","level":"INFO", "service":"api","msg":"Server started","port":8080}
{"ts":"2026-05-06T09:00:05Z","level":"DEBUG","service":"db", "msg":"Connection pool ready","pool_size":10}
{"ts":"2026-05-06T09:00:12Z","level":"INFO", "service":"api","msg":"Request received","method":"GET","path":"/users","latency_ms":4}
{"ts":"2026-05-06T09:00:12Z","level":"INFO", "service":"api","msg":"Request received","method":"POST","path":"/orders","latency_ms":18}
{"ts":"2026-05-06T09:00:15Z","level":"WARN", "service":"db", "msg":"Slow query","query_ms":250,"query":"SELECT * FROM orders"}
{"ts":"2026-05-06T09:00:20Z","level":"ERROR","service":"api","msg":"Unhandled exception","error":"NullReferenceException","path":"/orders"}
{"ts":"2026-05-06T09:00:21Z","level":"INFO", "service":"api","msg":"Request received","method":"GET","path":"/health","latency_ms":1}
{"ts":"2026-05-06T09:00:30Z","level":"ERROR","service":"db", "msg":"Connection lost","error":"Timeout","retry":true}
{"ts":"2026-05-06T09:00:35Z","level":"WARN", "service":"api","msg":"Rate limit approached","requests":950,"limit":1000}
{"ts":"2026-05-06T09:00:40Z","level":"INFO", "service":"api","msg":"Request received","method":"DELETE","path":"/sessions","latency_ms":6}
""";

// Aggregation state
var counts     = new Dictionary<string, int>(StringComparer.Ordinal);
var errors     = new List<(string ts, string service, string msg, string error)>();
var latencies  = new List<(string method, string path, long ms)>();
long totalLines = 0;

// Reuse a single parser for all lines — the internal buffer grows and is reused.
using var parser = new SimdJsonParser();

foreach (var line in ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    totalLines++;
    using var doc = parser.Parse(line);

    using var levelVal   = doc.GetField("level");
    using var tsVal      = doc.GetField("ts");
    using var serviceVal = doc.GetField("service");
    using var msgVal     = doc.GetField("msg");

    string level   = levelVal.GetString();
    string ts      = tsVal.GetString();
    string service = serviceVal.GetString();
    string msg     = msgVal.GetString();

    counts.TryGetValue(level, out int prev);
    counts[level] = prev + 1;

    if (level == "ERROR")
    {
        doc.Rewind();
        string errorText = "(unknown)";
        if (doc.TryGetField("error", out var errVal))
        {
            errorText = errVal!.GetString();
            errVal.Dispose();
        }
        errors.Add((ts, service, msg, errorText));
    }

    if (doc.TryGetField("latency_ms", out var latVal))
    {
        doc.Rewind();
        using var methodVal = doc.GetField("method");
        using var pathVal   = doc.GetField("path");
        latencies.Add((methodVal.GetString(), pathVal.GetString(), latVal!.GetInt64()));
        latVal.Dispose();
    }
}

// ── Report ────────────────────────────────────────────────────────────────
Console.WriteLine($"Processed {totalLines} log lines\n");

Console.WriteLine("── Log level counts ──");
foreach (var (level, count) in counts.OrderBy(kv => kv.Key))
{
    Console.WriteLine($"  {level,-6} : {count}");
}

Console.WriteLine();
Console.WriteLine($"── Errors ({errors.Count}) ──");
foreach (var (ts, svc, m, err) in errors)
{
    Console.WriteLine($"  [{ts}] {svc}: {m} — {err}");
}

Console.WriteLine();
Console.WriteLine("── Request latencies ──");
foreach (var (method, path, ms) in latencies)
{
    Console.WriteLine($"  {method,-6} {path,-12} {ms,4} ms");
}

if (latencies.Count > 0)
{
    double avg = latencies.Average(l => l.ms);
    long   max = latencies.Max(l => l.ms);
    Console.WriteLine($"  avg={avg:F1} ms, max={max} ms");
}
