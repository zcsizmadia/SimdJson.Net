using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using System.Text.Json;
using StjJsonDocument = System.Text.Json.JsonDocument;

namespace SimdJson.Benchmark.Benchmarks;

/// <summary>
/// Measures full document traversal: parse + iterate every token / element.
/// All three libraries: System.Text.Json, SimdJson.Net, SimdJsonSharp.Managed.
/// Note: SimdJsonSharp counts raw tape entries (structural tokens) while STJ and
/// SimdJson.Net count array elements, so absolute numbers differ — only timing matters.
/// </summary>
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public unsafe class FullTraversalBenchmarks
{
    [Params(JsonSize.Small, JsonSize.Medium, JsonSize.Large, JsonSize.XLarge)]
    public JsonSize Size { get; set; }

    private byte[] _data = null!;

    [GlobalSetup]
    public void Setup() => _data = JsonDataGenerator.Generate(Size);

    // ── System.Text.Json ────────────────────────────────────────────────────

    [BenchmarkCategory("FullTraversal")]
    [Benchmark(Baseline = true, Description = "STJ")]
    public int SystemTextJson()
    {
        int count = 0;
        var reader = new Utf8JsonReader(_data);
        while (reader.Read())
            count++;
        return count;
    }

    // ── SimdJson.Net ────────────────────────────────────────────────────────

    [BenchmarkCategory("FullTraversal")]
    [Benchmark(Description = "SimdJson.Net")]
    public int SimdJsonNet()
    {
        int count = 0;
        using var doc = SimdJsonParser.Shared.Parse(_data);

        if (doc.ValueKind == JsonValueKind.Array)
        {
            using var arr = doc.GetArray();
            foreach (var element in arr)
            {
                count++;
                element.Dispose();
            }
        }
        else
        {
            // Single object — iterate its fields
            using var obj = doc.GetObject();
            foreach (var field in obj)
            {
                count++;
                field.Value.Dispose();
            }
        }

        return count;
    }

    // ── SimdJsonSharp Managed ───────────────────────────────────────────────

    [BenchmarkCategory("FullTraversal")]
    [Benchmark(Description = "SimdJsonSharp")]
    public int SimdJsonSharpManaged()
    {
        if (!SimdJsonSharpAvailable) return 0;
        int count = 0;
        fixed (byte* ptr = _data)
        {
            using var iter = SimdJsonSharp.SimdJson.ParseJsonAndOpenIterator(ptr, _data.Length);
            if (iter.IsOk)
            {
                while (iter.MoveForward())
                    count++;
            }
        }
        return count;
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
