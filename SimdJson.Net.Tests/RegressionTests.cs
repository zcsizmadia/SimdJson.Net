namespace SimdJson.Tests;

// ─── Error-code translation ───────────────────────────────────────────────────

public class ErrorTranslationTests
{
    [Test]
    public async Task IsNull_OnMalformedAtom_ThrowsInsteadOfCrashing()
    {
        // "nax" starts like "null" but is not an atom; simdjson reports INCORRECT_TYPE.
        // The bridge must translate it rather than let a C++ exception escape.
        using var doc = SimdJsonParser.Shared.Parse("""{"a":nax}""");
        using var val = doc.GetField("a");
        await Assert.That(() => val.IsNull()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task TrailingContent_ReportsDedicatedErrorCode()
    {
        using var doc = SimdJsonParser.Shared.Parse("1 2");
        var ex = await Assert.That(() => doc.GetInt64()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-13);
    }

    [Test]
    public async Task NumberOutOfRange_ReportsDedicatedErrorCode()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":5000000000}""");
        using var val = doc.GetField("n");
        var ex = await Assert.That(() => val.GetInt32()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-10);
    }

    [Test]
    [Arguments(-10, "Number out of range.")]
    [Arguments(-11, "Native memory allocation failed.")]
    [Arguments(-12, "Maximum JSON nesting depth exceeded.")]
    [Arguments(-13, "Unexpected trailing content after the JSON value.")]
    public async Task SimdJsonException_NewCodes_HaveMessages(int code, string expected)
    {
        await Assert.That(new SimdJsonException(code).Message).IsEqualTo(expected);
    }
}

// ─── Raw token / raw string trimming ──────────────────────────────────────────

public class RawTokenTests
{
    [Test]
    public async Task GetRawJsonString_WithTrailingWhitespace_ReturnsOnlyStringBody()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s": "hi"  }""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetRawJsonString()).IsEqualTo("hi");
    }

    [Test]
    public async Task GetRawJsonString_EscapedContent_KeepsEscapes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"a\nb"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetRawJsonString()).IsEqualTo("a\\nb");
    }

    [Test]
    public async Task GetRawJsonToken_Number_WithTrailingWhitespace_IsTrimmed()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n": 1234 }""");
        using var val = doc.GetField("n");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("1234");
    }

    [Test]
    public async Task GetRawJsonToken_BigInteger_RoundTrips()
    {
        const string big = "123456789012345678901234567890";
        using var doc = SimdJsonParser.Shared.Parse($$"""{"n": {{big}} }""");
        using var val = doc.GetField("n");
        await Assert.That(val.GetNumberType()).IsEqualTo(JsonNumberType.BigInteger);
        await Assert.That(val.GetRawJsonToken()).IsEqualTo(big);
    }
}

// ─── Numbers ──────────────────────────────────────────────────────────────────

public class NumberConversionTests
{
    [Test]
    public async Task JsonNumber_AsDouble_BigInteger_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":123456789012345678901234567890}""");
        using var val = doc.GetField("n");
        var n = val.GetNumber();
        await Assert.That(() => n.AsDouble()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetDecimal_KeepsPrecisionBeyondDouble()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"d":123456789012345678901234.5}""");
        using var val = doc.GetField("d");
        await Assert.That(val.GetDecimal()).IsEqualTo(123456789012345678901234.5m);
    }

    [Test]
    public async Task GetDecimal_SmallValue_Works()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"d":3.14}""");
        using var val = doc.GetField("d");
        await Assert.That(val.GetDecimal()).IsEqualTo(3.14m);
    }

    [Test]
    public async Task GetDecimal_OutOfDecimalRange_ThrowsSimdJsonException()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"d":1e40}""");
        using var val = doc.GetField("d");
        var ex = await Assert.That(() => val.GetDecimal()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-10);
    }

    [Test]
    public async Task GetDecimal_NotANumber_ThrowsIncorrectType()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"x"}""");
        using var val = doc.GetField("s");
        var ex = await Assert.That(() => val.GetDecimal()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-2);
    }
}

// ─── Array.At index semantics ─────────────────────────────────────────────────

public class ArrayAtIndexTests
{
    [Test]
    public async Task At_IsAbsolute_NotRelativeToCursor()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[100,200,300]""");
        using var arr = doc.GetArray();
        using (var third = arr.At(2))
        {
            await Assert.That(third.GetInt64()).IsEqualTo(300L);
        }

        using var first = arr.At(0);
        await Assert.That(first.GetInt64()).IsEqualTo(100L);
    }

    [Test]
    public async Task At_SameIndexTwice_ReturnsSameValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[7,8,9]""");
        using var arr = doc.GetArray();
        using (var a = arr.At(1))
        {
            await Assert.That(a.GetInt64()).IsEqualTo(8L);
        }

        using var b = arr.At(1);
        await Assert.That(b.GetInt64()).IsEqualTo(8L);
    }
}
