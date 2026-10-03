using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class UInt64Tests
{
    [Test]
    public async Task GetUInt64_MaxValue_Works()
    {
        // ulong.MaxValue = 18446744073709551615
        using var doc = SimdJsonParser.Shared.Parse("""{"n":18446744073709551615}""");
        using var val = doc.GetField("n");
        await Assert.That(val.GetUInt64()).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    public async Task GetUInt64_Zero_Works()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":0}""");
        using var val = doc.GetField("n");
        await Assert.That(val.GetUInt64()).IsEqualTo(0UL);
    }
}

public class NumberTypeTests
{
    [Test]
    public async Task GetNumberType_Double_ReturnsFloatingPoint()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":3.14}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetNumberType()).IsEqualTo(JsonNumberType.FloatingPoint);
    }

    [Test]
    public async Task GetNumberType_NegativeInt_ReturnsSigned()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":-42}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetNumberType()).IsEqualTo(JsonNumberType.SignedInteger);
    }

    [Test]
    public async Task GetNumberType_LargeUnsigned_ReturnsUnsigned()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":18446744073709551615}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetNumberType()).IsEqualTo(JsonNumberType.UnsignedInteger);
    }

    [Test]
    public async Task GetNumberType_Zero_ReturnsSigned()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":0}""");
        using var val = doc.GetField("v");
        var t = val.GetNumberType();
        await Assert.That(t == JsonNumberType.SignedInteger || t == JsonNumberType.UnsignedInteger).IsTrue();
    }

    [Test]
    public async Task IsNegative_NegativeValue_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":-5}""");
        using var val = doc.GetField("v");
        await Assert.That(val.IsNegative()).IsTrue();
    }

    [Test]
    public async Task IsNegative_PositiveValue_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":5}""");
        using var val = doc.GetField("v");
        await Assert.That(val.IsNegative()).IsFalse();
    }

    [Test]
    public async Task IsNegative_FloatNegative_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":-1.5}""");
        using var val = doc.GetField("v");
        await Assert.That(val.IsNegative()).IsTrue();
    }

    [Test]
    public async Task IsInteger_IntValue_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":42}""");
        using var val = doc.GetField("v");
        await Assert.That(val.IsInteger()).IsTrue();
    }

    [Test]
    public async Task IsInteger_FloatValue_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":1.5}""");
        using var val = doc.GetField("v");
        await Assert.That(val.IsInteger()).IsFalse();
    }
}

public class BigIntegerTests
{
    [Test]
    [Arguments("18446744073709551616")]
    [Arguments("99999999999999999999")]
    public async Task NestedBigInteger_BoundaryRange_PreservesTypeAndDigits(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse($$"""{"value":{{json}}}""");
        using var value = doc.GetField("value");

        await Assert.That(value.GetNumberType()).IsEqualTo(JsonNumberType.BigInteger);
        await Assert.That(value.GetNumber().NumberType).IsEqualTo(JsonNumberType.BigInteger);
        await Assert.That(value.GetRawJsonToken()).IsEqualTo(json);
    }

    [Test]
    [Arguments("18446744073709551616")]
    [Arguments("99999999999999999999")]
    public async Task RootBigInteger_BoundaryRange_PreservesTypeAndDigits(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse(json);

        await Assert.That(doc.GetNumberType()).IsEqualTo(JsonNumberType.BigInteger);
        await Assert.That(doc.GetNumber().NumberType).IsEqualTo(JsonNumberType.BigInteger);
        await Assert.That(doc.GetRawJsonToken()).IsEqualTo(json);
    }

    [Test]
    public async Task RootBigInteger_AtUInt64Boundary_RemainsUnsignedInteger()
    {
        const string json = "18446744073709551615";
        using var doc = SimdJsonParser.Shared.Parse(json);

        await Assert.That(doc.GetNumberType()).IsEqualTo(JsonNumberType.UnsignedInteger);
        await Assert.That(doc.GetUInt64()).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    public async Task MalformedBigInteger_WithTrailingCharacter_IsRejectedAtRoot()
    {
        await Assert.That(() =>
        {
            using var doc = SimdJsonParser.Shared.Parse("123456789123456789123x");
            _ = doc.GetNumberType();
        }).Throws<SimdJsonException>();
    }

    [Test]
    public async Task MalformedBigInteger_WithTrailingCharacter_IsRejectedInObject()
    {
        await Assert.That(() =>
        {
            using var doc = SimdJsonParser.Shared.Parse("""{"value":123456789123456789123x}""");
            using var value = doc.GetField("value");
            _ = value.GetNumberType();
        }).Throws<SimdJsonException>();
    }
}

// ─── Raw JSON tests ──────────────────────────────────────────────────────────

public class NumberInStringTests
{
    [Test]
    public async Task GetDoubleInString_ValidString_ReturnsDouble()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"3.14"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetDoubleInString()).IsEqualTo(3.14);
    }

    [Test]
    public async Task GetInt64InString_ValidString_ReturnsInt()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"-42"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetInt64InString()).IsEqualTo(-42L);
    }

    [Test]
    public async Task GetUInt64InString_ValidString_ReturnsUInt()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"18446744073709551615"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetUInt64InString()).IsEqualTo(18446744073709551615UL);
    }

    [Test]
    public async Task TryGetDoubleInString_ValidString_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"2.71828"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.TryGetDoubleInString(out double d)).IsTrue();
        await Assert.That(d).IsEqualTo(2.71828);
    }

    [Test]
    public async Task TryGetInt64InString_InvalidString_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"not-a-number"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.TryGetInt64InString(out _)).IsFalse();
    }

    [Test]
    public async Task GetDoubleInString_OnRealNumber_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":3.14}""");
        using var val = doc.GetField("v");
        var ex = await Assert.That(() => val.GetDoubleInString()).Throws<SimdJsonException>();
        await Assert.That(ex).IsNotNull();
    }
}

// ─── AtPath tests ─────────────────────────────────────────────────────────────
