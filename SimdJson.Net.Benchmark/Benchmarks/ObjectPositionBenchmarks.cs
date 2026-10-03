using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;

namespace SimdJson.Benchmark.Benchmarks;

[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class ObjectPositionBenchmarks
{
    [Params(8, 128)]
    public int PrefixFields { get; set; }

    private byte[] _json = null!;
    private string[] _fieldNames = null!;

    [GlobalSetup]
    public void Setup()
    {
        var json = new StringBuilder("{");
        _fieldNames = new string[PrefixFields];
        for (int i = 0; i < PrefixFields; i++)
        {
            string name = _fieldNames[i] = $"field{i}";
            if (i != 0)
            {
                json.Append(',');
            }

            json.Append('"').Append(name).Append("\": ").Append(i);
        }

        json.Append(",\"target\":42}");
        _json = Encoding.UTF8.GetBytes(json.ToString());
    }

    [Benchmark(Baseline = true, Description = "Missing lookup + reset")]
    public long MissingLookupThenReset()
    {
        using var doc = SimdJsonParser.Shared.Parse(_json);
        using var obj = doc.GetObject();
        ConsumePrefix(obj);

        try
        {
            using var ignored = obj.FindField("optional");
        }
        catch (SimdJsonException)
        {
            obj.Reset();
        }

        using var target = obj.FindField("target");
        return target.GetInt64();
    }

    [Benchmark(Description = "Missing lookup + restore")]
    public long MissingLookupAndRestorePosition()
    {
        using var doc = SimdJsonParser.Shared.Parse(_json);
        using var obj = doc.GetObject();
        ConsumePrefix(obj);

        if (obj.TryFindField("optional", out var optional))
        {
            optional!.Dispose();
        }

        using var target = obj.FindField("target");
        return target.GetInt64();
    }

    private void ConsumePrefix(JsonObject obj)
    {
        foreach (string name in _fieldNames)
        {
            using var value = obj.FindField(name);
            _ = value.GetInt64();
        }
    }
}
