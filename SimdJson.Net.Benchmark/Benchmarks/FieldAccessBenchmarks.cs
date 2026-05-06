using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using StjValueKind = System.Text.Json.JsonValueKind;
using StjJsonDocument = System.Text.Json.JsonDocument;

namespace SimdJson.Benchmark.Benchmarks;

/// <summary>
/// Measures how quickly each library can parse and then access specific named fields
/// (email, age, active) from every element in an array of JSON objects.
/// Compares System.Text.Json vs SimdJson.Net (SimdJsonSharp has no high-level field API).
/// </summary>
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class FieldAccessBenchmarks
{
    [Params(JsonSize.Small, JsonSize.Medium, JsonSize.Large)]
    public JsonSize Size { get; set; }

    private byte[] _data = null!;

    [GlobalSetup]
    public void Setup() => _data = JsonDataGenerator.Generate(Size);

    // ── System.Text.Json ────────────────────────────────────────────────────

    [BenchmarkCategory("FieldAccess")]
    [Benchmark(Baseline = true, Description = "STJ")]
    public long SystemTextJson()
    {
        long sum = 0;
        using var doc = StjJsonDocument.Parse(_data);
        var root = doc.RootElement;

        if (root.ValueKind == StjValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                // Access three fields per element
                var email  = element.GetProperty("email").GetString();
                var age    = element.GetProperty("age").GetInt32();
                var active = element.GetProperty("active").GetBoolean();
                sum += age + (active ? 1 : 0) + (email?.Length ?? 0);
            }
        }
        else
        {
            // Single-object (Small) path
            var email  = root.GetProperty("email").GetString();
            var age    = root.GetProperty("age").GetInt32();
            var active = root.GetProperty("active").GetBoolean();
            sum += age + (active ? 1 : 0) + (email?.Length ?? 0);
        }

        return sum;
    }

    // ── SimdJson.Net ────────────────────────────────────────────────────────

    [BenchmarkCategory("FieldAccess")]
    [Benchmark(Description = "SimdJson.Net")]
    public long SimdJsonNet()
    {
        long sum = 0;
        using var doc = SimdJsonParser.Shared.Parse(_data);

        if (doc.ValueKind == JsonValueKind.Array)
        {
            using var arr = doc.GetArray();
            foreach (var element in arr)
            {
                using var objVal  = element.GetObject();
                using var emailV  = objVal.GetField("email");
                using var ageV    = objVal.GetField("age");
                using var activeV = objVal.GetField("active");

                var email  = emailV.GetString();
                var age    = ageV.GetInt64();
                var active = activeV.GetBool();
                sum += age + (active ? 1L : 0L) + email.Length;
                element.Dispose();
            }
        }
        else
        {
            using var obj     = doc.GetObject();
            using var emailV  = obj.GetField("email");
            using var ageV    = obj.GetField("age");
            using var activeV = obj.GetField("active");

            var email  = emailV.GetString();
            var age    = ageV.GetInt64();
            var active = activeV.GetBool();
            sum += age + (active ? 1L : 0L) + email.Length;
        }

        return sum;
    }
}
