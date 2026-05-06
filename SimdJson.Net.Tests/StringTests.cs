using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class UnicodeTests
{
    [Test]
    public async Task Unicode_Emoji_RoundTrips()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"e":"🎉"}""");
        using var val = doc.GetField("e");
        await Assert.That(val.GetString()).IsEqualTo("🎉");
    }

    [Test]
    public async Task Unicode_CJK_RoundTrips()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"t":"汉字"}""");
        using var val = doc.GetField("t");
        await Assert.That(val.GetString()).IsEqualTo("汉字");
    }

    [Test]
    public async Task Unicode_EscapeSequences_AreUnescaped()
    {
        // \u0041 = 'A', \u0042 = 'B'
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"\u0041\u0042"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetString()).IsEqualTo("AB");
    }

    [Test]
    public async Task Unicode_EscapedNewline_InString()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"line1\nline2"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetString()).IsEqualTo("line1\nline2");
    }
}

public class RawJsonTests
{
    [Test]
    public async Task GetRawJsonToken_Number_ReturnsDigits()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":42}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("42");
    }

    [Test]
    public async Task GetRawJsonToken_String_IncludesQuotes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"hello"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task GetRawJsonToken_Bool_ReturnsWord()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":true}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("true");
    }

    [Test]
    public async Task GetRawJsonToken_Null_ReturnsNull()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":null}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("null");
    }

    [Test]
    public async Task GetRawJsonTokenSpan_Number_ReturnsCorrectBytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":99}""");
        using var val = doc.GetField("v");
        var span = val.GetRawJsonTokenSpan();
        await Assert.That(System.Text.Encoding.UTF8.GetString(span)).IsEqualTo("99");
    }

    [Test]
    public async Task GetRawJson_Object_ReturnsFullJson()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"obj":{"a":1,"b":2}}""");
        using var val = doc.GetField("obj");
        var raw = val.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"a":1,"b":2}""");
    }

    [Test]
    public async Task GetRawJson_Array_ReturnsFullJson()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"arr":[1,2,3]}""");
        using var val = doc.GetField("arr");
        var raw = val.GetRawJson();
        await Assert.That(raw).IsEqualTo("[1,2,3]");
    }

    [Test]
    public async Task ArrayGetRawJson_ReturnsFullArray()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[10,20,30]""");
        using var arr = doc.GetArray();
        var raw = arr.GetRawJson();
        await Assert.That(raw).IsEqualTo("[10,20,30]");
    }

    [Test]
    public async Task ObjectGetRawJson_ReturnsFullObject()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1,"y":2}""");
        using var obj = doc.GetObject();
        var raw = obj.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"x":1,"y":2}""");
    }

    [Test]
    public async Task DocumentGetRawJson_ReturnsFullDocument()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var raw = doc.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"a":1}""");
    }
}

// ─── Numbers in strings tests ─────────────────────────────────────────────────

public class RawJsonStringTests
{
    [Test]
    public async Task GetRawJsonString_SimpleString_ReturnsEscapedBytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"hello"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetRawJsonString()).IsEqualTo("hello");
    }

    [Test]
    public async Task GetRawJsonString_EscapedNewline_ReturnsTwoByteEscape()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"a\nb"}""");
        using var val = doc.GetField("s");
        // raw token is "a\nb" — backslash + n, not a real newline
        string raw = val.GetRawJsonString();
        await Assert.That(raw).IsEqualTo(@"a\nb");
    }

    [Test]
    public async Task GetRawJsonString_EmptyString_ReturnsEmptyString()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":""}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetRawJsonString()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task GetRawJsonStringSpan_MatchesGetString_ForPlainAscii()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"simdjson"}""");
        using var val = doc.GetField("s");
        ReadOnlySpan<byte> span = val.GetRawJsonStringSpan();
        string fromSpan = System.Text.Encoding.UTF8.GetString(span);
        await Assert.That(fromSpan).IsEqualTo("simdjson");
    }

    [Test]
    public async Task GetRawJsonString_NonStringValue_ThrowsSimdJsonException()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":42}""");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetRawJsonString()).Throws<SimdJsonException>();
    }
}

public class RawJsonSpanTests
{
    [Test]
    public async Task ArrayGetRawJsonSpan_MatchesGetRawJson()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"arr":[1,2,3]}""");
        using var arr = doc.GetField("arr").GetArray();
        var str = arr.GetRawJson();
        // Re-open to get span (GetRawJson consumes)
        using var doc2 = SimdJsonParser.Shared.Parse("""{"arr":[1,2,3]}""");
        using var arr2 = doc2.GetField("arr").GetArray();
        var span = arr2.GetRawJsonSpan();
        await Assert.That(System.Text.Encoding.UTF8.GetString(span)).IsEqualTo(str);
    }

    [Test]
    public async Task ObjectGetRawJsonSpan_MatchesGetRawJson()
    {
        const string json = """{"a":1,"b":2}""";
        using var doc = SimdJsonParser.Shared.Parse(json);
        using var obj = doc.GetObject();
        var str = obj.GetRawJson();
        using var doc2 = SimdJsonParser.Shared.Parse(json);
        using var obj2 = doc2.GetObject();
        var span = obj2.GetRawJsonSpan();
        await Assert.That(System.Text.Encoding.UTF8.GetString(span)).IsEqualTo(str);
    }
}

// ─── NdjsonParser ─────────────────────────────────────────────────────────────
