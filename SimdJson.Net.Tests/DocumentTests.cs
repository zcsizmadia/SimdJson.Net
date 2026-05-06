using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class DocumentTests
{
    private const string SampleJson = """
        {
            "name": "Alice",
            "age": 30,
            "active": true,
            "score": 9.5,
            "alias": null,
            "tags": ["admin","user"],
            "address": { "city": "Budapest" }
        }
        """;

    [Test]
    public async Task GetField_String_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.GetField("name");
        await Assert.That(val.GetString()).IsEqualTo("Alice");
    }

    [Test]
    public async Task GetField_Int64_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.GetField("age");
        await Assert.That(val.GetInt64()).IsEqualTo(30L);
    }

    [Test]
    public async Task GetField_Bool_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.GetField("active");
        await Assert.That(val.GetBool()).IsTrue();
    }

    [Test]
    public async Task GetField_Double_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.GetField("score");
        await Assert.That(val.GetDouble()).IsEqualTo(9.5);
    }

    [Test]
    public async Task GetField_Null_IsNull()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.GetField("alias");
        await Assert.That(val.IsNull()).IsTrue();
    }

    [Test]
    public async Task Indexer_ReturnsField()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc["name"];
        await Assert.That(val.GetString()).IsEqualTo("Alice");
    }

    [Test]
    public async Task GetField_MissingKey_ThrowsSimdJsonException()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        await Assert.That(() => { using var v = doc.GetField("missing"); })
            .Throws<SimdJsonException>();
    }

    [Test]
    public async Task AtPointer_DeepField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.AtPointer("/address/city");
        await Assert.That(val.GetString()).IsEqualTo("Budapest");
    }

    [Test]
    public async Task AtPointer_ArrayElement_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse(SampleJson);
        using var val = doc.AtPointer("/tags/0");
        await Assert.That(val.GetString()).IsEqualTo("admin");
    }
}

public class DocumentGetValueTests
{
    [Test]
    public async Task DocumentGetValue_ObjectRoot_ReturnsObjectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
        using var val = doc.GetValue();
        await Assert.That(val.ValueKind).IsEqualTo(JsonValueKind.Object);
    }

    [Test]
    public async Task DocumentGetValue_ArrayRoot_ReturnsArrayValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        using var val = doc.GetValue();
        await Assert.That(val.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    [Test]
    public async Task DocumentGetValue_ScalarRoot_Throws()
    {
        // simdjson does not support get_value() on scalar-root documents
        using var doc = SimdJsonParser.Shared.Parse("""42""");
        await Assert.That(() => doc.GetValue()).Throws<SimdJsonException>();
    }
}

// ─── CurrentDepth tests ───────────────────────────────────────────────────────

public class DocumentScalarGettersTests
{
    [Test]
    public async Task GetString_ScalarRootString()
    {
        using var doc = SimdJsonParser.Shared.Parse(""""  "hello"  """");
        await Assert.That(doc.GetString()).IsEqualTo("hello");
    }

    [Test]
    public async Task GetBool_True()
    {
        using var doc = SimdJsonParser.Shared.Parse("true");
        await Assert.That(doc.GetBool()).IsTrue();
    }

    [Test]
    public async Task IsNull_NullRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse("null");
        await Assert.That(doc.IsNull()).IsTrue();
    }

    [Test]
    public async Task GetDouble_ScalarRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse("3.14");
        await Assert.That(doc.GetDouble()).IsEqualTo(3.14);
    }

    [Test]
    public async Task GetInt64_ScalarRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse("-42");
        await Assert.That(doc.GetInt64()).IsEqualTo(-42L);
    }

    [Test]
    public async Task GetUInt64_ScalarRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse("18446744073709551615");
        await Assert.That(doc.GetUInt64()).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    public async Task GetInt64InString_ScalarRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse(""""  "-99"  """");
        await Assert.That(doc.GetInt64InString()).IsEqualTo(-99L);
    }

    [Test]
    public async Task GetDoubleInString_ScalarRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse(""""  "2.718"  """");
        await Assert.That(doc.GetDoubleInString()).IsEqualTo(2.718);
    }

    [Test]
    public async Task GetUInt64InString_ScalarRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse(""""  "100"  """");
        await Assert.That(doc.GetUInt64InString()).IsEqualTo(100UL);
    }

    [Test]
    public async Task GetString_AllowReplacement_ReturnsString()
    {
        using var doc = SimdJsonParser.Shared.Parse(""""  "valid"  """");
        await Assert.That(doc.GetString(allowReplacement: true)).IsEqualTo("valid");
    }

    [Test]
    public async Task At_ReturnsCorrectElement()
    {
        using var doc = SimdJsonParser.Shared.Parse("[10,20,30]");
        using var val = doc.At(1);
        await Assert.That(val.GetInt64()).IsEqualTo(20L);
    }

    [Test]
    public async Task CountElements_RootArray()
    {
        using var doc = SimdJsonParser.Shared.Parse("[1,2,3,4,5]");
        await Assert.That(doc.CountElements()).IsEqualTo(5);
    }

    [Test]
    public async Task CountFields_RootObject()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        await Assert.That(doc.CountFields()).IsEqualTo(3);
    }

    [Test]
    public async Task GetRawJsonSpan_MatchesGetRawJson()
    {
        const string json = """{"x":1}""";
        using var doc = SimdJsonParser.Shared.Parse(json);
        var str = doc.GetRawJson();
        var span = doc.GetRawJsonSpan();
        await Assert.That(System.Text.Encoding.UTF8.GetString(span)).IsEqualTo(str);
    }
}

// ─── JsonValue count and native int32/uint32 ─────────────────────────────────

public class DocumentNumberHelpersTests
{
    [Test]
    public async Task DocumentGetNumberType_FloatRoot_ReturnsFloating()
    {
        using var doc = SimdJsonParser.Shared.Parse("3.14");
        await Assert.That(doc.GetNumberType()).IsEqualTo(JsonNumberType.FloatingPoint);
    }

    [Test]
    public async Task DocumentGetNumberType_IntRoot_ReturnsSigned()
    {
        using var doc = SimdJsonParser.Shared.Parse("42");
        await Assert.That(doc.GetNumberType()).IsEqualTo(JsonNumberType.SignedInteger);
    }

    [Test]
    public async Task DocumentIsNegative_NegativeNumber_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("-7");
        await Assert.That(doc.IsNegative()).IsTrue();
    }

    [Test]
    public async Task DocumentIsNegative_PositiveNumber_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("5");
        await Assert.That(doc.IsNegative()).IsFalse();
    }

    [Test]
    public async Task DocumentIsInteger_WholeNumber_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("100");
        await Assert.That(doc.IsInteger()).IsTrue();
    }

    [Test]
    public async Task DocumentIsInteger_FloatNumber_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("1.5");
        await Assert.That(doc.IsInteger()).IsFalse();
    }

    [Test]
    public async Task DocumentGetNumber_FloatRoot_ReturnsFloatingPointNumber()
    {
        using var doc = SimdJsonParser.Shared.Parse("2.5");
        var n = doc.GetNumber();
        await Assert.That(n.NumberType).IsEqualTo(JsonNumberType.FloatingPoint);
        await Assert.That(n.AsDouble()).IsEqualTo(2.5);
    }

    [Test]
    public async Task DocumentGetNumber_IntRoot_ReturnsSignedInteger()
    {
        using var doc = SimdJsonParser.Shared.Parse("123");
        var n = doc.GetNumber();
        await Assert.That(n.NumberType).IsEqualTo(JsonNumberType.SignedInteger);
        await Assert.That(n.AsInt64()).IsEqualTo(123L);
    }

    [Test]
    public async Task DocumentGetRawJsonToken_NumberRoot_ReturnsLiteral()
    {
        using var doc = SimdJsonParser.Shared.Parse("42");
        await Assert.That(doc.GetRawJsonToken()).IsEqualTo("42");
    }

    [Test]
    public async Task DocumentGetRawJsonToken_StringRoot_ReturnsQuotedLiteral()
    {
        using var doc = SimdJsonParser.Shared.Parse("\"hello\"");
        // raw token includes the surrounding quotes
        await Assert.That(doc.GetRawJsonToken()).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task DocumentGetRawJsonTokenSpan_NumberRoot_ReturnsBytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("99");
        var span = doc.GetRawJsonTokenSpan();
        await Assert.That(Encoding.UTF8.GetString(span)).IsEqualTo("99");
    }
}

public class CurrentDepthTests
{
    [Test]
    public async Task DocumentCurrentDepth_AfterParse_IsAtLeastOne()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        // simdjson initialises the iterator at depth 1 (the document root level)
        await Assert.That(doc.CurrentDepth()).IsGreaterThan(0);
    }

    [Test]
    public async Task ValueCurrentDepth_NestedValue_IsGreaterThanZero()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":1}}""");
        using var outer = doc.GetField("a");
        using var inner = outer.GetField("b");
        // After accessing a nested value the depth is > 0
        await Assert.That(inner.CurrentDepth()).IsGreaterThan(0);
    }
}

// ─── Document extra coverage tests ─────────────────────────────────────────────

public class DocumentExtraStringTests
{
    [Test]
    public async Task GetString_AllowReplacementFalse_RedirectsToGetString()
    {
        using var doc = SimdJsonParser.Shared.Parse("\"hello\"");
        await Assert.That(doc.GetString(allowReplacement: false)).IsEqualTo("hello");
    }

    [Test]
    public async Task GetStringSpan_RootString_ReturnsUtf8Bytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("\"world\"");
        var span = doc.GetStringSpan();
        await Assert.That(Encoding.UTF8.GetString(span)).IsEqualTo("world");
    }

    [Test]
    public async Task GetWobblyStringSpan_RootString_ReturnsBytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("\"hello\"");
        var span = doc.GetWobblyStringSpan();
        await Assert.That(Encoding.UTF8.GetString(span)).IsEqualTo("hello");
    }

    [Test]
    public async Task CurrentOffset_AfterParse_IsNonNegative()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
        var offset = doc.CurrentOffset();
        await Assert.That((long)(nuint)offset).IsGreaterThanOrEqualTo(0L);
    }

    [Test]
    public async Task TryGetField_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var ok = doc.TryGetField("missing", out var v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsNull();
    }

    [Test]
    public async Task TryAtPointer_MissingPath_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var ok = doc.TryAtPointer("/missing", out var v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsNull();
    }
}

// ─── Parser configuration tests ──────────────────────────────────────────────
