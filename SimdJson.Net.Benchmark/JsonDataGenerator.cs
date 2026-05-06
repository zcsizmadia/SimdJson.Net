using System.Globalization;
using System.Text;

namespace SimdJson.Benchmark;

/// <summary>
/// Generates synthetic JSON payloads of four standard sizes:
///   Small   ~    1 KB  (single object)
///   Medium  ~  100 KB  (array of objects)
///   Large   ~   10 MB  (array of objects)
///   XLarge  ~  100 MB  (array of objects)
/// </summary>
public enum JsonSize { Small, Medium, Large, XLarge }

public static class JsonDataGenerator
{
    // Record sizes are ~400 bytes each
    private static readonly int[] RecordCounts = [1, 250, 25_000, 250_000];

    /// <summary>Returns pre-generated UTF-8 JSON bytes for the requested size.</summary>
    public static byte[] Generate(JsonSize size)
    {
        int count = RecordCounts[(int)size];
        return BuildArray(count);
    }

    /// <summary>Approximate byte length of a generated payload (useful for throughput reporting).</summary>
    public static long ApproximateByteLength(JsonSize size) =>
        RecordCounts[(int)size] * 400L;

    // ── builders ────────────────────────────────────────────────────────────

    private static byte[] BuildArray(int count)
    {
        // Pre-allocate: ~420 bytes per record + brackets + commas
        var sb = new StringBuilder(count * 420 + 4);
        sb.Append('[');
        for (int i = 1; i <= count; i++)
        {
            if (i > 1) sb.Append(',');
            AppendRecord(sb, i);
        }
        sb.Append(']');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void AppendRecord(StringBuilder sb, int id)
    {
        int age = 20 + (id % 50);
        double score = Math.Round((id % 100) + 0.5 + (id * 0.001 % 1), 4);
        bool active = (id & 1) == 0;
        int version = (id % 5) + 1;
        string firstName = $"FirstName{id:D7}";
        string lastName = $"LastName{id:D7}";
        string zip = $"{60000 + (id % 10000):D5}";

        sb.Append("{\"id\":");
        sb.Append(id);
        sb.Append(",\"firstName\":\"");
        sb.Append(firstName);
        sb.Append("\",\"lastName\":\"");
        sb.Append(lastName);
        sb.Append("\",\"age\":");
        sb.Append(age);
        sb.Append(",\"email\":\"");
        sb.Append(firstName.ToLowerInvariant());
        sb.Append('.');
        sb.Append(lastName.ToLowerInvariant());
        sb.Append("@example.com\",\"active\":");
        sb.Append(active ? "true" : "false");
        sb.Append(",\"score\":");
        sb.Append(score.ToString("F4", CultureInfo.InvariantCulture));
        sb.Append(",\"tags\":[\"admin\",\"user\",\"moderator\"],\"address\":{\"street\":\"");
        sb.Append(id);
        sb.Append(" Main Street\",\"city\":\"Springfield\",\"state\":\"IL\",\"zip\":\"");
        sb.Append(zip);
        sb.Append("\",\"country\":\"USA\"},\"metadata\":{\"createdAt\":\"2024-01-15T10:30:00Z\",\"version\":");
        sb.Append(version);
        sb.Append("}}");
    }
}
