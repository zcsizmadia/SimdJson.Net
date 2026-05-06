using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using StjJsonDocument = System.Text.Json.JsonDocument;

namespace SimdJson.Benchmark.Benchmarks;

/// <summary>
/// Measures the raw parse throughput of each library (parse only — no value extraction).
/// All three libraries: System.Text.Json, SimdJson.Net, SimdJsonSharp.Managed.
/// </summary>
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public unsafe class ParseOnlyBenchmarks
{
    [Params(JsonSize.Small, JsonSize.Medium, JsonSize.Large, JsonSize.XLarge)]
    public JsonSize Size { get; set; }

    private byte[] _data = null!;

    [GlobalSetup]
    public void Setup() => _data = JsonDataGenerator.Generate(Size);

    // ── System.Text.Json ────────────────────────────────────────────────────

    [BenchmarkCategory("ParseOnly")]
    [Benchmark(Baseline = true, Description = "STJ")]
    public void SystemTextJson()
    {
        using var doc = StjJsonDocument.Parse(_data);
        _ = doc.RootElement.ValueKind;
    }

    // ── SimdJson.Net ────────────────────────────────────────────────────────

    [BenchmarkCategory("ParseOnly")]
    [Benchmark(Description = "SimdJson.Net")]
    public void SimdJsonNet()
    {
        using var doc = SimdJsonParser.Shared.Parse(_data);
        _ = doc.ValueKind;
    }

    // ── SimdJsonSharp Managed ───────────────────────────────────────────────

    [BenchmarkCategory("ParseOnly")]
    [Benchmark(Description = "SimdJsonSharp")]
    public void SimdJsonSharpManaged()
    {
        if (!SimdJsonSharpAvailable)
        {
            return;
        }

        fixed (byte* ptr = _data)
        {
            using var parsed = SimdJsonSharp.SimdJson.ParseJson(ptr, _data.Length, false);
            _ = parsed.IsValid;
        }
    }

    private static readonly bool SimdJsonSharpAvailable = CheckSimdJsonSharp();
    private static bool CheckSimdJsonSharp()
    {
        try
        {
            byte[] probe = "{}"u8.ToArray();
            unsafe { fixed (byte* p = probe) { using var d = SimdJsonSharp.SimdJson.ParseJson(p, probe.Length, false); } }
            return true;
        }
        catch { return false; }
    }
}

