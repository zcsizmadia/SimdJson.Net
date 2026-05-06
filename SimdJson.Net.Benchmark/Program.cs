using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using SimdJson.Benchmark;
using SimdJson.Benchmark.Benchmarks;

// ── quick smoke-test when not in Release mode ────────────────────────────────
if (!IsReleaseBuild())
{
    Console.WriteLine("NOTE: Run with 'dotnet run -c Release' for actual benchmark numbers.");
    Console.WriteLine("Running quick validation instead...\n");
    RunSmokeTest();
    return;
}

// ── BenchmarkDotNet runner ───────────────────────────────────────────────────
var config = DefaultConfig.Instance
    .AddColumn(StatisticColumn.Min, StatisticColumn.Max)
    .HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)
    .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);

// ── helpers ──────────────────────────────────────────────────────────────────

static bool IsReleaseBuild()
{
#if DEBUG
    return false;
#else
    return true;
#endif
}

static void RunSmokeTest()
{
    foreach (var size in Enum.GetValues<JsonSize>())
    {
        var data = JsonDataGenerator.Generate(size);
        Console.Write($"  {size,-8} {data.Length / 1024.0,6:F0} KB  →  ");

        // STJ
        using (var doc = System.Text.Json.JsonDocument.Parse(data))
            Console.Write($"STJ:{doc.RootElement.ValueKind}  ");

        // SimdJson.Net
        using (var doc = SimdJson.SimdJsonParser.Shared.Parse(data))
            Console.Write($"SimdJson.Net:{doc.ValueKind}  ");

        // SimdJsonSharp — requires AVX2; skip gracefully if unavailable
        try
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    using var parsed = SimdJsonSharp.SimdJson.ParseJson(ptr, data.Length, false);
                    Console.Write($"SimdJsonSharp:valid={parsed.IsValid}");
                }
            }
        }
        catch (NotSupportedException ex)
        {
            Console.Write($"SimdJsonSharp:skipped ({ex.Message})");
        }

        Console.WriteLine("  ✓");
    }

    Console.WriteLine("\nAll available parsers handled all sizes successfully.");
    Console.WriteLine($"simdjson native version: {SimdJson.SimdJsonParser.GetVersion()}");
}
