// 07 – Zero-Allocation / Low-Allocation Patterns
// Demonstrates GetStringSpan (avoids string allocation), Parse(ReadOnlySpan<byte>)
// (no transcoding), GetRawJsonTokenSpan, and UTF-8 comparison without allocation.

using System.Text;
using SimdJson;

Console.WriteLine("=== 07 – Zero-Allocation Patterns ===\n");

// ── 1. Parse from UTF-8 literal — no transcoding ─────────────────────────
// The u8 suffix produces a ReadOnlySpan<byte> directly from the binary.
Console.WriteLine("── Parse UTF-8 literal (no transcoding) ──");
using var doc = SimdJsonParser.Shared.Parse("""{"id":42,"tag":"hot"}"""u8);
using var id  = doc.GetField("id");
Console.WriteLine($"id : {id.GetInt64()}");

// ── 2. GetStringSpan — read string without allocating a managed string ─────
Console.WriteLine();
Console.WriteLine("── GetStringSpan (no managed string allocation) ──");
doc.Rewind();
using var tag     = doc.GetField("tag");
ReadOnlySpan<byte> tagSpan = tag.GetStringSpan();  // backed by native buffer
Console.WriteLine($"tag bytes: [{string.Join(", ", tagSpan.ToArray())}]");
Console.WriteLine($"tag UTF-8: {Encoding.UTF8.GetString(tagSpan)}");

// ── 3. UTF-8 comparison without allocating a string ───────────────────────
Console.WriteLine();
Console.WriteLine("── Span comparison without allocation ──");
ReadOnlySpan<byte> expected = "hot"u8;
bool isHot = tagSpan.SequenceEqual(expected);
Console.WriteLine($"tag equals 'hot': {isHot}");

// ── 4. GetRawJsonTokenSpan — raw token as bytes ───────────────────────────
Console.WriteLine();
Console.WriteLine("── GetRawJsonTokenSpan ──");
using var doc2     = SimdJsonParser.Shared.Parse("""{"price":9.99}""");
using var price    = doc2.GetField("price");
ReadOnlySpan<byte> tokenSpan = price.GetRawJsonTokenSpan();
Console.WriteLine($"raw token bytes : [{string.Join(", ", tokenSpan.ToArray())}]");
Console.WriteLine($"raw token text  : {Encoding.UTF8.GetString(tokenSpan)}");

// ── 5. Reuse the parser across many parses — buffer grows, never shrinks ──
Console.WriteLine();
Console.WriteLine("── Parser reuse (single growing buffer) ──");
using var parser = new SimdJsonParser();
long sum = 0;
for (int i = 0; i < 5; i++)
{
    using var d = parser.Parse($"{{\"value\":{i * 100}}}");
    using var v = d.GetField("value");
    sum += v.GetInt64();
}
Console.WriteLine($"Sum of 5 parses : {sum}");   // 0+100+200+300+400 = 1000
Console.WriteLine($"Parser capacity : {parser.Capacity} bytes");

// ── 6. GetCurrentOffset / GetCurrentDepth for diagnostics ─────────────────
Console.WriteLine();
Console.WriteLine("── CurrentOffset / CurrentDepth ──");
using var doc3   = SimdJsonParser.Shared.Parse("""{"outer":{"inner":99}}""");
using var outer  = doc3.GetField("outer").GetObject();
using var inner  = outer.GetField("inner");
Console.WriteLine($"inner value     : {inner.GetInt64()}");
Console.WriteLine($"doc offset      : {doc3.CurrentOffset()}");
Console.WriteLine($"doc depth       : {doc3.CurrentDepth()}");

// ── 7. ValidateUtf8 without parsing ──────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── ValidateUtf8 ──");
ReadOnlySpan<byte> validUtf8   = "hello 🌍"u8;
ReadOnlySpan<byte> invalidUtf8 = [0xFF, 0xFE, 0x00];
Console.WriteLine($"valid UTF-8   : {SimdJsonParser.ValidateUtf8(validUtf8)}");
Console.WriteLine($"invalid UTF-8 : {SimdJsonParser.ValidateUtf8(invalidUtf8)}");

// ── 8. Minify — remove whitespace without parsing ─────────────────────────
Console.WriteLine();
Console.WriteLine("── Minify ──");
const string pretty = """
{
  "a": 1,
  "b": 2
}
""";
string minified = SimdJsonParser.Minify(pretty);
Console.WriteLine($"minified: {minified}");
