using System.Globalization;
using System.IO;
using System.Text;

namespace SimdJson.Net.Benchmarks;

/// <summary>
/// Generates synthetic JSON payloads in well-defined size buckets used by
/// <see cref="ParseBenchmarks"/>.
///
/// Size targets:
/// <list type="bullet">
///   <item><c>small</c>  ≈ 1 KB</item>
///   <item><c>medium</c> ≈ 256 KB</item>
///   <item><c>large</c>  ≈ 5 MB</item>
///   <item><c>vlarge</c> ≈ 25 MB</item>
///   <item><c>xlarge</c> ≈ 120 MB (satisfies the 100 MB+ requirement)</item>
/// </list>
///
/// Generated payloads are deterministic (seeded RNG) and cached on disk under
/// <c>%TEMP%\SimdJson.Net.bench-data\</c> so successive benchmark runs reuse
/// the same bytes — important for the larger sizes where generation cost
/// would otherwise dominate setup.
/// </summary>
internal static class SampleData
{
    private const int OneKB = 1024;
    private const int OneMB = 1024 * 1024;

    public static byte[] GetUtf8(string size)
    {
        int target = TargetBytes(size);
        int seed   = SeedFor(size);

        string cacheDir  = Path.Combine(Path.GetTempPath(), "SimdJson.Net.bench-data");
        Directory.CreateDirectory(cacheDir);
        string cacheFile = Path.Combine(cacheDir, $"{size}-{target}-{seed}.json");

        if (File.Exists(cacheFile))
        {
            var info = new FileInfo(cacheFile);
            if (info.Length >= target) return File.ReadAllBytes(cacheFile);
        }

        BuildToFile(cacheFile, target, seed);
        return File.ReadAllBytes(cacheFile);
    }

    private static int TargetBytes(string size) => size switch
    {
        "small"  => 1 * OneKB,
        "medium" => 256 * OneKB,
        "large"  => 5 * OneMB,
        "vlarge" => 25 * OneMB,
        "xlarge" => 120 * OneMB,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    private static int SeedFor(string size) => size switch
    {
        "small"  => 1,
        "medium" => 2,
        "large"  => 3,
        "vlarge" => 4,
        "xlarge" => 5,
        _ => 0,
    };

    /// <summary>
    /// Streams the document straight to disk so we never need to keep a
    /// 100+ MB <see cref="StringBuilder"/> in memory during generation.
    /// </summary>
    private static void BuildToFile(string path, long targetBytes, int seed)
    {
        var rng = new Random(seed);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 20, useAsync: false);
        using var w = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        w.Write("{\"records\":[");
        bool first = true;
        long i = 0;
        var rec = new StringBuilder(512);

        while (fs.Position < targetBytes)
        {
            rec.Clear();
            if (!first) rec.Append(',');
            first = false;
            AppendRecord(rec, rng, i++);
            w.Write(rec.ToString());
            // Flush periodically so fs.Position reflects bytes written.
            if ((i & 0xFFF) == 0) w.Flush();
        }
        w.Write("]}");
        w.Flush();
    }

    private static void AppendRecord(StringBuilder sb, Random rng, long id)
    {
        sb.Append('{')
          .Append("\"id\":").Append(id).Append(',')
          .Append("\"text\":\"").Append(EscapedText(rng)).Append("\",")
          .Append("\"user\":{\"id\":").Append(id * 7)
          .Append(",\"name\":\"user_").Append(id)
          .Append("\",\"verified\":").Append(rng.Next(2) == 0 ? "false" : "true")
          .Append("},")
          .Append("\"hashtags\":[");
        int tags = rng.Next(0, 5);
        for (int t = 0; t < tags; t++)
        {
            if (t > 0) sb.Append(',');
            sb.Append("\"tag").Append(rng.Next(10000)).Append('"');
        }
        sb.Append("],")
          .Append("\"retweet_count\":").Append(rng.Next(50_000)).Append(',')
          .Append("\"favorite_count\":").Append(rng.Next(100_000)).Append(',')
          .Append("\"score\":").Append((rng.NextDouble() * 100).ToString("F4", CultureInfo.InvariantCulture)).Append(',')
          .Append("\"coords\":[")
          .Append((rng.NextDouble() * 360 - 180).ToString("F6", CultureInfo.InvariantCulture)).Append(',')
          .Append((rng.NextDouble() * 180 - 90).ToString("F6", CultureInfo.InvariantCulture))
          .Append("],")
          .Append("\"truncated\":").Append(rng.Next(2) == 0 ? "false" : "true").Append(',')
          .Append("\"lang\":\"en\",")
          .Append("\"meta\":null")
          .Append('}');
    }

    private static readonly string[] Words =
    {
        "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "lorem", "ipsum", "dolor", "sit", "amet", "json", "simd", "vector",
        "perf", "throughput", "modern", "dotnet", "span", "memory",
    };

    private static string EscapedText(Random rng)
    {
        int n = rng.Next(3, 12);
        var sb = new StringBuilder(n * 8);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Words[rng.Next(Words.Length)]);
        }
        if (rng.Next(8) == 0) sb.Append(" \\\"q\\\"");
        return sb.ToString();
    }
}
