using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class ValueKindTests
{
    [Test]
    [Arguments("""[1]""", JsonValueKind.Array)]
    [Arguments("""{"a":1}""", JsonValueKind.Object)]
    [Arguments("42", JsonValueKind.Number)]
    [Arguments("\"a string\"", JsonValueKind.String)]
    [Arguments("true", JsonValueKind.Boolean)]
    [Arguments("null", JsonValueKind.Null)]
    public async Task DocumentRoot_HasCorrectKind(string json, JsonValueKind expected)
    {
        using var doc = SimdJsonParser.Shared.Parse(json);
        await Assert.That(doc.ValueKind).IsEqualTo(expected);
    }
}

public class SpanTests
{
    [Test]
    public async Task Parse_Utf8Span_Works()
    {
        var utf8 = """{"hello":"world"}"""u8;
        using var doc = SimdJsonParser.Shared.Parse(utf8);
        using var val = doc.GetField("hello");
        await Assert.That(val.GetString()).IsEqualTo("world");
    }

    [Test]
    public async Task GetStringSpan_ReturnsUtf8Bytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"k":"hello"}""");
        using var val = doc.GetField("k");
        var span = val.GetStringSpan();
        await Assert.That(Encoding.UTF8.GetString(span)).IsEqualTo("hello");
    }
}

public class TypePredicateTests
{
    [Test]
    public async Task ValueIsScalar_OnNumber_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":42}""");
        using var val = doc.GetField("n");
        await Assert.That(val.IsScalar()).IsTrue();
    }

    [Test]
    public async Task ValueIsScalar_OnArray_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":[1,2]}""");
        using var val = doc.GetField("a");
        await Assert.That(val.IsScalar()).IsFalse();
    }

    [Test]
    public async Task ValueIsString_OnString_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"hello"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.IsString()).IsTrue();
    }

    [Test]
    public async Task ValueIsString_OnNumber_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":1}""");
        using var val = doc.GetField("n");
        await Assert.That(val.IsString()).IsFalse();
    }

    [Test]
    public async Task DocumentIsScalar_OnScalarRoot_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""42""");
        await Assert.That(doc.IsScalar()).IsTrue();
    }

    [Test]
    public async Task DocumentIsScalar_OnObjectRoot_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
        await Assert.That(doc.IsScalar()).IsFalse();
    }

    [Test]
    public async Task DocumentIsString_OnStringRoot_ReturnsTrue()
    {
        // JSON: "hello"  (a root-level JSON string value)
        using var doc = SimdJsonParser.Shared.Parse("\"hello\"");
        await Assert.That(doc.IsString()).IsTrue();
    }

    [Test]
    public async Task ArrayIsEmpty_OnEmptyArray_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[]""");
        using var arr = doc.GetArray();
        await Assert.That(arr.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task ArrayIsEmpty_OnNonEmptyArray_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        using var arr = doc.GetArray();
        await Assert.That(arr.IsEmpty()).IsFalse();
    }
}

// ─── GetNumber (structured number) tests ─────────────────────────────────────

public class GetNumberTests
{
    [Test]
    public async Task GetNumber_Float_ReturnsFloatingPoint()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":3.14}""");
        using var val = doc.GetField("x");
        var num = val.GetNumber();
        await Assert.That(num.NumberType).IsEqualTo(JsonNumberType.FloatingPoint);
        await Assert.That(num.AsDouble()).IsEqualTo(3.14);
    }

    [Test]
    public async Task GetNumber_Negative_ReturnsSigned()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":-99}""");
        using var val = doc.GetField("x");
        var num = val.GetNumber();
        await Assert.That(num.NumberType).IsEqualTo(JsonNumberType.SignedInteger);
        await Assert.That(num.AsInt64()).IsEqualTo(-99L);
    }

    [Test]
    public async Task GetNumber_LargeUnsigned_ReturnsUnsigned()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":18446744073709551615}""");
        using var val = doc.GetField("x");
        var num = val.GetNumber();
        await Assert.That(num.NumberType).IsEqualTo(JsonNumberType.UnsignedInteger);
        await Assert.That(num.AsUInt64()).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    public async Task GetNumber_ToString_ReturnsRepresentation()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":7}""");
        using var val = doc.GetField("x");
        var num = val.GetNumber();
        await Assert.That(num.ToString()).IsEqualTo("7");
    }
}

// ─── DocumentGetValue tests ───────────────────────────────────────────────────
// document.get_value() in simdjson works for OBJECT and ARRAY roots only.
// Scalar roots return SCALAR_DOCUMENT_AS_VALUE; those should throw.

public class ValueCountAndNativeIntTests
{
    [Test]
    public async Task CountElements_OnValueArray()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"arr":[1,2,3]}""");
        using var val = doc.GetField("arr");
        await Assert.That(val.CountElements()).IsEqualTo(3);
    }

    [Test]
    public async Task CountFields_OnValueObject()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"obj":{"a":1,"b":2}}""");
        using var val = doc.GetField("obj");
        await Assert.That(val.CountFields()).IsEqualTo(2);
    }

    [Test]
    public async Task GetInt32_Native_InRange()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":42}""");
        using var val = doc.GetField("n");
        await Assert.That(val.GetInt32()).IsEqualTo(42);
    }

    [Test]
    public async Task GetInt32_Native_ThrowsOnOverflow()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":3000000000}""");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetInt32()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task GetUInt32_Native_InRange()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":4294967295}""");
        using var val = doc.GetField("n");
        await Assert.That(val.GetUInt32()).IsEqualTo(uint.MaxValue);
    }

    [Test]
    public async Task GetUInt32_Native_ThrowsOnNegative()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":-1}""");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetUInt32()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task TryGetInt32_ReturnsFalseOnOverflow()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":3000000000}""");
        using var val = doc.GetField("n");
        await Assert.That(val.TryGetInt32(out _)).IsFalse();
    }

    [Test]
    public async Task TryGetUInt32_ReturnsTrueInRange()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":99}""");
        using var val = doc.GetField("n");
        bool ok = val.TryGetUInt32(out uint result);
        await Assert.That(ok).IsTrue();
        await Assert.That(result).IsEqualTo(99u);
    }

    [Test]
    public async Task GetString_AllowReplacement_ReturnsString()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"hello"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetString(allowReplacement: true)).IsEqualTo("hello");
    }

    [Test]
    public async Task GetStringSpan_AllowReplacement_MatchesGetString()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"world"}""");
        using var val = doc.GetField("s");
        var span = val.GetStringSpan(allowReplacement: true);
        await Assert.That(System.Text.Encoding.UTF8.GetString(span)).IsEqualTo("world");
    }
}

// ─── GetRawJsonSpan on array and object ──────────────────────────────────────
