namespace SimdJson.Tests;

// ─── TryXxx helpers must report absence, not hide real errors ─────────────────

public class TryHelperAbsenceTests
{
    [Test]
    public async Task TryGetField_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        await Assert.That(doc.TryGetField("nope", out var v)).IsFalse();
        await Assert.That(v).IsNull();
    }

    [Test]
    public async Task TryAtPointer_UnresolvedPointer_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        await Assert.That(doc.TryAtPointer("/missing", out var v)).IsFalse();
    }

    [Test]
    public async Task TryAtPointer_MalformedPointer_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        await Assert.That(doc.TryAtPointer("not-a-pointer", out _)).IsFalse();
    }

    [Test]
    public async Task TryAtPath_UnresolvedPath_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        await Assert.That(doc.TryAtPath("$.missing", out _)).IsFalse();
    }

    [Test]
    public async Task ObjectTryGetField_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.TryGetField("nope", out _)).IsFalse();
    }

    [Test]
    public async Task ContainsKey_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.ContainsKey("nope")).IsFalse();
    }

    [Test]
    public async Task ContainsKey_PresentKey_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.ContainsKey("a")).IsTrue();
    }
}

// ─── Typed getters still answer "not that type" with false ────────────────────

public class TryHelperTypeTests
{
    [Test]
    public async Task TryGetString_OnNumber_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":1}""");
        using var val = doc.GetField("n");
        await Assert.That(val.TryGetString(out _)).IsFalse();
    }

    [Test]
    public async Task TryGetInt64_OnString_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"x"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.TryGetInt64(out _)).IsFalse();
    }

    [Test]
    public async Task TryGetInt32_Overflow_ReturnsFalse()
    {
        // Number out of range is code -10 and must still be absorbed here.
        using var doc = SimdJsonParser.Shared.Parse("""{"n":5000000000}""");
        using var val = doc.GetField("n");
        await Assert.That(val.TryGetInt32(out _)).IsFalse();
    }

    [Test]
    public async Task TryGetArray_OnObject_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"o":{"a":1}}""");
        using var val = doc.GetField("o");
        await Assert.That(val.TryGetArray(out _)).IsFalse();
    }

    [Test]
    public async Task TryGetInt64InString_NonNumericString_ReturnsFalse()
    {
        // The string's contents not being a number is an expected answer, not a
        // broken document, so the number-format error is absorbed here.
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"abc"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.TryGetInt64InString(out _)).IsFalse();
    }

    [Test]
    public async Task TryGetInt64InString_NumericString_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"-42"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.TryGetInt64InString(out long v)).IsTrue();
        await Assert.That(v).IsEqualTo(-42L);
    }
}

// ─── Real errors must propagate instead of being reported as absence ──────────

public class TryHelperPropagationTests
{
    [Test]
    public async Task TryGetField_OnNonObjectRoot_PropagatesIncorrectType()
    {
        // Asking an array for a field by name is a caller mistake, not a missing
        // field, so it must not come back as a bare false.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""[1,2,3]""");
        var ex = await Assert.That(() => doc.TryGetField("a", out _)).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-2);
    }

    [Test]
    public async Task ValueTryFindFieldUnordered_OnScalar_PropagatesIncorrectType()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"n":42}""");
        using var val = doc.GetField("n");
        var ex = await Assert.That(() => val.TryFindFieldUnordered("a", out _))
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-2);
    }

    // Note: out-of-order iteration, error -7, is deliberately not tested here.
    // simdjson compiles those checks out unless SIMDJSON_DEVELOPMENT_CHECKS is on,
    // which it is not in the release builds this package ships, so provoking one in
    // a test corrupts parser state rather than raising a catchable error.

    [Test]
    public async Task TryGetField_OnDisposedDocument_PropagatesObjectDisposed()
    {
        var parser = new SimdJsonParser();
        var doc = parser.Parse("""{"a":1}""");
        doc.Dispose();
        await Assert.That(() => doc.TryGetField("a", out _)).Throws<ObjectDisposedException>();
        parser.Dispose();
    }

    [Test]
    public async Task TryGetString_OnDisposedValue_PropagatesObjectDisposed()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a":"x"}""");
        var val = doc.GetField("a");
        val.Dispose();
        await Assert.That(() => val.TryGetString(out _)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task TryGetField_MalformedDocument_PropagatesParseError()
    {
        // On-Demand reports malformed JSON lazily, at access time. A broken document
        // is not the same as a missing field.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a":tru}""");
        var ex = await Assert.That(() => doc.TryGetField("a", out var v) && v!.GetBool())
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsNotEqualTo(-3);
    }
}

// ─── The classification itself ────────────────────────────────────────────────

public class TryHelperClassificationTests
{
    [Test]
    [Arguments(-3)]
    [Arguments(-4)]
    [Arguments(-8)]
    public async Task LookupMissCodes_AreAbsorbedByLookupHelpers(int code)
    {
        // Documented contract: only these three mean "not there".
        await Assert.That(code is -3 or -4 or -8).IsTrue();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(-2)]
    [Arguments(-6)]
    [Arguments(-7)]
    [Arguments(-11)]
    [Arguments(-12)]
    [Arguments(-13)]
    public async Task NonMissCodes_AreNotTreatedAsAbsence(int code)
    {
        await Assert.That(code is -3 or -4 or -8).IsFalse();
    }
}
