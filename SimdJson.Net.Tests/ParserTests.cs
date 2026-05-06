using System.Collections;
using System.Text;

namespace SimdJson.Tests;

using System.IO;

public class ParserTests
{
    [Test]
    public async Task Parse_SimpleObject_ReturnsObject()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice","age":30}""");
        await Assert.That(doc.ValueKind).IsEqualTo(JsonValueKind.Object);
    }

    [Test]
    public async Task Parse_SimpleArray_ReturnsArray()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        await Assert.That(doc.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    [Test]
    public async Task Parse_InvalidJson_ThrowsSimdJsonException()
    {
        // simdjson on-demand is lazy: Parse succeeds, error surfaces on first access
        using var doc = SimdJsonParser.Shared.Parse("{bad}");
        await Assert.That(() => doc.GetField("x"))
            .Throws<SimdJsonException>();
    }

    [Test]
    public async Task ParseAsync_String_ReturnsDocument()
    {
        using var parser = new SimdJsonParser();
        using var doc = await parser.ParseAsync("""{"x":1}""");
        await Assert.That(doc.ValueKind).IsEqualTo(JsonValueKind.Object);
    }

    [Test]
    public async Task ParseAsync_Stream_ReturnsDocument()
    {
        var json = """{"stream":true}"""u8.ToArray();
        using var ms = new System.IO.MemoryStream(json);
        using var parser = new SimdJsonParser();
        using var doc = await parser.ParseAsync(ms);
        using var val = doc.GetField("stream");
        await Assert.That(val.GetBool()).IsTrue();
    }

    [Test]
    public async Task GetVersion_ReturnsSimdJsonVersion()
    {
        var version = SimdJsonParser.GetVersion();
        await Assert.That(version).IsNotEmpty();
        // simdjson version string is semver e.g. "4.6.3"
        await Assert.That(version).Contains(".");
    }

    [Test]
    public async Task Parse_LargeStringJson_Works()
    {
        // Forces ArrayPool path (> 4096 bytes for UTF-8 transcoding buffer)
        var padding = new string('x', 5000);
        var json = $$$"""{"pad":"{{{padding}}}"}""";
        using var doc = SimdJsonParser.Shared.Parse(json);
        using var val = doc.GetField("pad");
        await Assert.That(val.GetString()).IsEqualTo(padding);
    }
}

public class ParserConfigurationTests
{
    [Test]
    public async Task CreateParser_DefaultMaxCapacity_IsPositive()
    {
        using var parser = new SimdJsonParser();
        await Assert.That(parser.MaxCapacity).IsGreaterThan((nuint)0);
    }

    [Test]
    public async Task CreateParser_WithCapacity_SetMaxCapacity()
    {
        nuint cap = 1024 * 1024; // 1 MiB
        using var parser = new SimdJsonParser(cap);
        await Assert.That(parser.MaxCapacity).IsEqualTo(cap);
    }

    [Test]
    public async Task ParserMaxDepth_IsPositive()
    {
        using var parser = new SimdJsonParser();
        await Assert.That(parser.MaxDepth).IsGreaterThan((nuint)0);
    }

    [Test]
    public async Task ParserCapacity_AfterParse_IsPositive()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"x":1}""");
        await Assert.That(parser.Capacity).IsGreaterThan((nuint)0);
    }

    [Test]
    public async Task ParserMaxCapacity_Setter_ChangesValue()
    {
        using var parser = new SimdJsonParser();
        nuint newCap = 512 * 1024;
        parser.MaxCapacity = newCap;
        await Assert.That(parser.MaxCapacity).IsEqualTo(newCap);
    }
}

// ─── Utility method coverage tests ──────────────────────────────────────────────

public class ValidateUtf8ExtraTests
{
    [Test]
    public async Task ValidateUtf8_StringOverload_ValidText_ReturnsTrue()
    {
        await Assert.That(SimdJsonParser.ValidateUtf8("hello world")).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_StringOverload_EmptyString_ReturnsTrue()
    {
        await Assert.That(SimdJsonParser.ValidateUtf8(string.Empty)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_EmptyByteSpan_ReturnsTrue()
    {
        await Assert.That(SimdJsonParser.ValidateUtf8(Array.Empty<byte>())).IsTrue();
    }

    [Test]
    public async Task ParseAllowIncompleteJson_LargeString_UsesArrayPoolPath()
    {
        // > 4096 bytes triggers the ArrayPool branch in ParseAllowIncompleteJson(string)
        var padding = new string('z', 5000);

        var jsonStr = "{\"key\":\"" + padding + "\"}";
        using var parser = new SimdJsonParser();
        using var doc = parser.ParseAllowIncompleteJson(jsonStr);
        using var val = doc.GetField("key");
        await Assert.That(val.GetString()).IsEqualTo(padding);
    }
}

// ─── Array AtPointer / AtPath tests ──────────────────────────────────────────

public class RewindTests
{
    [Test]
    public async Task Rewind_AllowsReReadingFirstField()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":10,"y":20}""");
        // First read
        using var val1 = doc.GetField("x");
        await Assert.That(val1.GetInt64()).IsEqualTo(10L);
        val1.Dispose();

        // Rewind and read again
        doc.Rewind();
        using var val2 = doc.GetField("x");
        await Assert.That(val2.GetInt64()).IsEqualTo(10L);
    }

    [Test]
    public async Task Rewind_AfterIteratingAllFields_AllowsReRead()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        // Read all fields
        using var obj = doc.GetObject();
        int count = 0;
        foreach (var prop in obj)
        {
            count++;
            prop.Value.Dispose();
        }
        await Assert.That(count).IsEqualTo(3);
        obj.Dispose();

        // Rewind and verify document is accessible again
        doc.Rewind();
        using var val = doc.GetField("a");
        await Assert.That(val.GetInt64()).IsEqualTo(1L);
    }
}

// ─── Array.At tests ───────────────────────────────────────────────────────────
