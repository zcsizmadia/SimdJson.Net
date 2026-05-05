using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace SimdJson.Net.Benchmarks;

/// <summary>
/// Compares <c>SimdJson.Net</c> against <c>SimdJsonSharp</c> (managed C# port)
/// and <see cref="System.Text.Json"/> across five payload sizes:
/// small (1 KB), medium (256 KB), large (5 MB), vlarge (25 MB) and xlarge
/// (120 MB).
/// <para>
/// xlarge is excluded from default runs because it requires &gt;1 GB of
/// working set across all parsers. Opt in with:
/// <c>dotnet run -c Release -- --filter '*ParseBenchmarks*' --anyCategories XLarge</c>.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class ParseBenchmarks
{
    private byte[] _utf8 = null!;

    /// <summary>
    /// Payload size bucket. xlarge is opt-in via <c>--anyCategories XLarge</c>.
    /// </summary>
    [ParamsAllValues]
    public PayloadSize Size { get; set; }

    [GlobalSetup]
    public void Setup() => _utf8 = SampleData.GetUtf8(Size.ToToken());

    // -- System.Text.Json baseline --------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Small", "Medium", "Large", "VLarge", "XLarge")]
    public long SystemTextJson()
    {
        using var d = System.Text.Json.JsonDocument.Parse(_utf8);
        return CountElements(d.RootElement);
    }

    private static long CountElements(System.Text.Json.JsonElement e)
    {
        long n = 1;
        switch (e.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) n += CountElements(p.Value);
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var v in e.EnumerateArray()) n += CountElements(v);
                break;
        }
        return n;
    }

    // -- SimdJsonSharp (managed C#) -------------------------------------
    // Requires AVX2. On hardware without AVX2 the call would throw a
    // PlatformNotSupportedException; we guard it and return -1 so the row
    // is preserved but obviously meaningless. Filter it out with
    // --anyCategories Net to skip on non-AVX2 boxes.

    [Benchmark]
    [BenchmarkCategory("Small", "Medium", "Large", "VLarge", "XLarge")]
    public unsafe long SimdJsonSharp_Managed()
    {
        if (!Avx2.IsSupported) return -1;
        long count = 0;
        fixed (byte* p = _utf8)
        {
            using var doc = SimdJsonSharp.SimdJson.ParseJson(p, _utf8.Length);
            using var iter = doc.CreateIterator();
            while (iter.MoveForward()) count++;
        }
        return count;
    }

    // -- SimdJson.Net ----------------------------------------------------

    [Benchmark]
    [BenchmarkCategory("Small", "Medium", "Large", "VLarge", "XLarge")]
    public long SimdJsonNet()
    {
        using var doc = JsonDocument.Parse(_utf8);
        return Walk(doc.Root);
    }

    private static long Walk(JsonElement e)
    {
        long n = 1;
        switch (e.ValueKind)
        {
            case JsonElementType.Object:
                foreach (var p in e.EnumerateObject()) n += Walk(p.Value);
                break;
            case JsonElementType.Array:
                foreach (var v in e.EnumerateArray()) n += Walk(v);
                break;
        }
        return n;
    }
}

/// <summary>Payload size bucket for <see cref="ParseBenchmarks"/>.</summary>
public enum PayloadSize
{
    Small,
    Medium,
    Large,
    VLarge,
    XLarge,
}

internal static class PayloadSizeExtensions
{
    public static string ToToken(this PayloadSize s) => s switch
    {
        PayloadSize.Small  => "small",
        PayloadSize.Medium => "medium",
        PayloadSize.Large  => "large",
        PayloadSize.VLarge => "vlarge",
        PayloadSize.XLarge => "xlarge",
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };
}
