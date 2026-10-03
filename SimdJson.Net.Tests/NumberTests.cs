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

public class NarrowIntegerTests
{
    [Test]
    [Arguments("-128", (sbyte)-128)]
    [Arguments("127", (sbyte)127)]
    public async Task GetSByte_Boundaries_ReturnValues(string json, sbyte expected)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(val.GetSByte()).IsEqualTo(expected);
        await Assert.That(val.TryGetSByte(out var actual)).IsTrue();
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("0", (byte)0)]
    [Arguments("255", (byte)255)]
    public async Task GetByte_Boundaries_ReturnValues(string json, byte expected)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(val.GetByte()).IsEqualTo(expected);
        await Assert.That(val.TryGetByte(out var actual)).IsTrue();
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("-32768", (short)-32768)]
    [Arguments("32767", (short)32767)]
    public async Task GetInt16_Boundaries_ReturnValues(string json, short expected)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(val.GetInt16()).IsEqualTo(expected);
        await Assert.That(val.TryGetInt16(out var actual)).IsTrue();
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("0", (ushort)0)]
    [Arguments("65535", (ushort)65535)]
    public async Task GetUInt16_Boundaries_ReturnValues(string json, ushort expected)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(val.GetUInt16()).IsEqualTo(expected);
        await Assert.That(val.TryGetUInt16(out var actual)).IsTrue();
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("-129")]
    [Arguments("128")]
    public async Task GetSByte_OutOfRange_Throws(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        var ex = await Assert.That(() => val.GetSByte()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-10);
    }

    [Test]
    [Arguments("-1")]
    [Arguments("256")]
    public async Task GetByte_NegativeOrOutOfRange_Throws(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetByte()).Throws<SimdJsonException>();
    }

    [Test]
    [Arguments("-32769")]
    [Arguments("32768")]
    public async Task GetInt16_OutOfRange_Throws(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetInt16()).Throws<SimdJsonException>();
    }

    [Test]
    [Arguments("-1")]
    [Arguments("65536")]
    public async Task GetUInt16_NegativeOrOutOfRange_Throws(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{json}}}");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetUInt16()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task NarrowGetters_RejectFractionsAndWrongTypes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"fraction":1.5,"text":"1"}""");
        using var fraction = doc.GetField("fraction");
        using var text = doc.GetField("text");
        await Assert.That(() => fraction.GetInt16()).Throws<SimdJsonException>();
        await Assert.That(() => text.GetByte()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task TryGetNarrowIntegers_ReturnsFalseForRangeAndTypeErrors()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"tooLarge":128,"negative":-1,"fraction":1.5}""");
        using var tooLarge = doc.GetField("tooLarge");
        using var negative = doc.GetField("negative");
        using var fraction = doc.GetField("fraction");
        await Assert.That(tooLarge.TryGetSByte(out _)).IsFalse();
        await Assert.That(negative.TryGetByte(out _)).IsFalse();
        await Assert.That(fraction.TryGetInt16(out _)).IsFalse();
        await Assert.That(fraction.TryGetUInt16(out _)).IsFalse();
    }
}

public class DirectFloatTests
{
    [Test]
    public async Task GetFloat_ParsesValueDirectlyToSinglePrecision()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":1.000000059604644775390626}""");
        using var value = doc.GetField("n");
        await Assert.That(BitConverter.SingleToInt32Bits(value.GetFloat())).IsEqualTo(0x3F800001);
    }

    [Test]
    public async Task GetFloat_ParsesDecimalDirectlyToSinglePrecision()
    {
        using var doc = SimdJsonParser.Shared.Parse("1.000000059604644775390626");
        await Assert.That(BitConverter.SingleToInt32Bits(doc.GetFloat())).IsEqualTo(0x3F800001);
    }

    [Test]
    public async Task GetFloat_ParsesExponentNotation()
    {
        using var doc = SimdJsonParser.Shared.Parse("1.25e2");
        await Assert.That(doc.GetFloat()).IsEqualTo(125f);
    }

    [Test]
    public async Task GetFloat_OutsideFloatRange_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("1e39");
        await Assert.That(() => doc.GetFloat()).Throws<SimdJsonException>();
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
