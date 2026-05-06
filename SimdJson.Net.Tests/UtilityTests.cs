using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class MinifyTests
{
    [Test]
    public async Task Minify_RemovesWhitespace()
    {
        var result = SimdJsonParser.Minify("""{ "a" : 1, "b" : 2 }""");
        await Assert.That(result).IsEqualTo("""{"a":1,"b":2}""");
    }

    [Test]
    public async Task Minify_AlreadyMinified_ReturnsSame()
    {
        var json = """{"x":1}""";
        var result = SimdJsonParser.Minify(json);
        await Assert.That(result).IsEqualTo(json);
    }

    [Test]
    public async Task Minify_Nested_RemovesAllWhitespace()
    {
        var result = SimdJsonParser.Minify("{\n  \"arr\": [\n    1,\n    2,\n    3\n  ]\n}");
        await Assert.That(result).IsEqualTo("""{"arr":[1,2,3]}""");
    }

    [Test]
    public async Task MinifyUtf8_ProducesCorrectBytes()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("""{ "k" : "v" }""");
        var result = SimdJsonParser.MinifyUtf8(input);
        var str = System.Text.Encoding.UTF8.GetString(result);
        await Assert.That(str).IsEqualTo("""{"k":"v"}""");
    }

    [Test]
    public async Task Minify_PreservesStringsWithSpaces()
    {
        var result = SimdJsonParser.Minify("""{ "msg" : "hello world" }""");
        await Assert.That(result).IsEqualTo("""{"msg":"hello world"}""");
    }
}

// ─── UTF-8 validation tests ───────────────────────────────────────────────────

public class Utf8ValidationTests
{
    [Test]
    public async Task ValidateUtf8_AsciiBytes_ReturnsTrue()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("Hello, world!");
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_ValidMultibyteBytes_ReturnsTrue()
    {
        // "こんにちは" is valid UTF-8
        var bytes = System.Text.Encoding.UTF8.GetBytes("こんにちは");
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_InvalidBytes_ReturnsFalse()
    {
        // 0xFF is not valid in any UTF-8 sequence
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0xFF };
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsFalse();
    }

    [Test]
    public async Task ValidateUtf8_EmptySpan_ReturnsTrue()
    {
        await Assert.That(SimdJsonParser.ValidateUtf8(ReadOnlySpan<byte>.Empty)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_OverlongEncoding_ReturnsFalse()
    {
        // Overlong encoding of U+0000 (2-byte): 0xC0, 0x80
        var bytes = new byte[] { 0xC0, 0x80 };
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsFalse();
    }

    [Test]
    public async Task ValidateUtf8_ValidEmojiBytes_ReturnsTrue()
    {
        // 😀 = U+1F600 encoded as 4-byte UTF-8: F0 9F 98 80
        var bytes = new byte[] { 0xF0, 0x9F, 0x98, 0x80 };
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsTrue();
    }
}

// ─── Object Reset tests ───────────────────────────────────────────────────────
