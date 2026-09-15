using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;

namespace SimdJson.Benchmark.Benchmarks;

/// <summary>
/// Compares the two ways of reading NDJSON that this library offers: the incremental
/// line-splitting reader, and simdjson's batching parser over an in-memory buffer.
/// </summary>
/// <remarks>
/// They are not interchangeable. The batching parser needs the whole input resident and
/// indexes it in batches; the line reader streams with bounded memory. This measures what
/// the batching buys where it applies.
/// </remarks>
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class NdjsonBenchmarks
{
    [Params(1_000, 50_000)]
    public int Documents { get; set; }

    private byte[] _ndjson = null!;

    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Documents; i++)
        {
            sb.Append("{\"id\":").Append(i)
              .Append(",\"name\":\"item-").Append(i)
              .Append("\",\"score\":").Append(i * 1.5)
              .Append(",\"active\":").Append(i % 2 == 0 ? "true" : "false")
              .Append("}\n");
        }

        _ndjson = Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static long SelectId(JsonDocument doc)
    {
        using var v = doc.GetField("id");
        return v.GetInt64();
    }

    /// <summary>The incremental reader: splits lines itself and parses one document per call.</summary>
    [Benchmark(Baseline = true, Description = "Sequential (stream)")]
    public async Task<long> Sequential()
    {
        long sum = 0;
        using var stream = new MemoryStream(_ndjson);
        await foreach (var id in NdjsonParser.ParseAsync(stream, SelectId,
            new NdjsonParserOptions { LeaveOpen = true }))
        {
            sum += id;
        }

        return sum;
    }

    /// <summary>simdjson iterate_many over the whole buffer.</summary>
    [Benchmark(Description = "Batching (iterate_many)")]
    public long Batching()
    {
        long sum = 0;
        using var stream = NdjsonParser.OpenStream(_ndjson);
        while (stream.MoveNext())
        {
            sum += SelectId(stream.Current!);
        }

        return sum;
    }

    /// <summary>The batching parser through the list-returning convenience overload.</summary>
    [Benchmark(Description = "Batching (Parse)")]
    public long BatchingParse()
    {
        long sum = 0;
        foreach (long id in NdjsonParser.Parse(_ndjson, SelectId))
        {
            sum += id;
        }

        return sum;
    }
}
