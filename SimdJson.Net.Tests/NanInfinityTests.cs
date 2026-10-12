using System.Text;

namespace SimdJson.Tests;

public class NanInfinityStrictTests
{
    [Test]
    [Arguments("NaN")]
    [Arguments("nan")]
    [Arguments("Infinity")]
    [Arguments("-Infinity")]
    [Arguments("inf")]
    [Arguments("-inf")]
    public async Task DefaultParser_RejectsNonFiniteTokens(string token)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":{token}}}");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetDouble()).Throws<SimdJsonException>();
        await Assert.That(() => val.GetFloat()).Throws<SimdJsonException>();
        await Assert.That(() => val.GetNumber()).Throws<SimdJsonException>();
        await Assert.That(val.TryGetDouble(out _)).IsFalse();
        await Assert.That(val.TryGetFloat(out _)).IsFalse();
    }

    [Test]
    public async Task DefaultParser_ErrorCodesMatchStrictBehaviour()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"n":NaN}""");
        using var val = doc.GetField("n");
        await Assert.That(Assert.Throws<SimdJsonException>(() => val.GetDouble()).ErrorCode).IsEqualTo(-2);
        await Assert.That(Assert.Throws<SimdJsonException>(() => val.GetFloat()).ErrorCode).IsEqualTo(-2);
        await Assert.That(Assert.Throws<SimdJsonException>(() => val.GetNumber()).ErrorCode).IsEqualTo(-6);
    }

    [Test]
    [Arguments("NaN")]
    [Arguments("-Infinity")]
    public async Task DefaultParser_RejectsNonFiniteStrings(string token)
    {
        using var doc = SimdJsonParser.Shared.Parse($"{{\"n\":\"{token}\"}}");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetDoubleInString()).Throws<SimdJsonException>();
        await Assert.That(val.TryGetDoubleInString(out _)).IsFalse();
    }

    [Test]
    public async Task DefaultParser_RejectsNonFiniteRoot()
    {
        using var doc = SimdJsonParser.Shared.Parse("NaN");
        await Assert.That(() => doc.GetDouble()).Throws<SimdJsonException>();
        await Assert.That(() => doc.GetFloat()).Throws<SimdJsonException>();
        await Assert.That(() => doc.GetNumber()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task DefaultParser_FlagIsOff()
    {
        using var parser = new SimdJsonParser();
        await Assert.That(parser.AllowNanInfinity).IsFalse();
        await Assert.That(SimdJsonParser.Shared.AllowNanInfinity).IsFalse();
    }

    [Test]
    public async Task OutOfRangeNumbers_StillErrorWhenAllowed()
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse("""{"n":1e999}""");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetDouble()).Throws<SimdJsonException>();
    }
}

public class NanInfinityAllowedTests
{
    [Test]
    [Arguments("NaN")]
    [Arguments("nan")]
    [Arguments("NAN")]
    public async Task Nan_IsParsed(string token)
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse($"{{\"n\":{token}}}");
        using var val = doc.GetField("n");
        await Assert.That(double.IsNaN(val.GetDouble())).IsTrue();
        await Assert.That(float.IsNaN(val.GetFloat())).IsTrue();
        await Assert.That(double.IsNaN(val.GetNumber().AsDouble())).IsTrue();
        await Assert.That(val.TryGetDouble(out var d) && double.IsNaN(d)).IsTrue();
    }

    [Test]
    [Arguments("Infinity", true)]
    [Arguments("inf", true)]
    [Arguments("INFINITY", true)]
    [Arguments("-Infinity", false)]
    [Arguments("-inf", false)]
    public async Task Infinity_IsParsedWithSign(string token, bool positive)
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse($"{{\"n\":{token}}}");
        using var val = doc.GetField("n");
        double expected = positive ? double.PositiveInfinity : double.NegativeInfinity;
        await Assert.That(val.GetDouble()).IsEqualTo(expected);
        await Assert.That(val.GetFloat()).IsEqualTo((float)expected);
        var number = val.GetNumber();
        await Assert.That(number.NumberType).IsEqualTo(JsonNumberType.FloatingPoint);
        await Assert.That(number.AsDouble()).IsEqualTo(expected);
    }

    [Test]
    public async Task NonFiniteStrings_AreParsedByGetDoubleInString()
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse("""{"a":"NaN","b":"-Infinity"}""");
        using var a = doc.GetField("a");
        using var b = doc.GetField("b");
        await Assert.That(double.IsNaN(a.GetDoubleInString())).IsTrue();
        await Assert.That(b.GetDoubleInString()).IsEqualTo(double.NegativeInfinity);
    }

    [Test]
    public async Task RootScalar_IsParsed()
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using (var doc = parser.Parse("-Infinity"))
        {
            await Assert.That(doc.GetDouble()).IsEqualTo(double.NegativeInfinity);
            await Assert.That(doc.GetNumber().AsDouble()).IsEqualTo(double.NegativeInfinity);
        }
        using (var doc = parser.Parse("NaN"))
        {
            await Assert.That(double.IsNaN(doc.GetDouble())).IsTrue();
            await Assert.That(double.IsNaN(doc.GetFloat())).IsTrue();
        }
    }

    [Test]
    public async Task MixedWithFiniteNumbers_ArrayAndObjectIteration()
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse("""{"xs":[1.5,NaN,-Infinity,2],"after":7}""");
        using var xs = doc.GetField("xs");
        using var arr = xs.GetArray();
        var seen = new List<double>();
        foreach (var item in arr)
        {
            using (item) seen.Add(item.GetDouble());
        }
        await Assert.That(seen.Count).IsEqualTo(4);
        await Assert.That(seen[0]).IsEqualTo(1.5);
        await Assert.That(double.IsNaN(seen[1])).IsTrue();
        await Assert.That(seen[2]).IsEqualTo(double.NegativeInfinity);
        await Assert.That(seen[3]).IsEqualTo(2.0);
        using var after = doc.GetField("after");
        await Assert.That(after.GetInt64()).IsEqualTo(7L);
    }

    [Test]
    public async Task WildcardCallbackValues_HonourTheFlag()
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse("""{"xs":[NaN,Infinity]}""");
        var seen = new List<double>();
        doc.ForEachAtPath("$.xs[*]", v => seen.Add(v.GetDouble()));
        await Assert.That(seen.Count).IsEqualTo(2);
        await Assert.That(double.IsNaN(seen[0])).IsTrue();
        await Assert.That(seen[1]).IsEqualTo(double.PositiveInfinity);
    }

    [Test]
    public async Task WildcardCallbackValues_AreStrictByDefault()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"xs":[NaN]}""");
        await Assert.That(() => doc.ForEachAtPath("$.xs[*]", v => v.GetDouble())).Throws<SimdJsonException>();
    }

    [Test]
    [Arguments("+Infinity")]
    [Arguments("NaNx")]
    [Arguments("Nan1")]
    [Arguments("Infinit")]
    public async Task MalformedTokens_AreStillRejected(string token)
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse($"{{\"n\":{token}}}");
        using var val = doc.GetField("n");
        await Assert.That(() => val.GetDouble()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task FlagIsCapturedWhenTheDocumentIsCreated()
    {
        using var parser = new SimdJsonParser();
        using (var strict = parser.Parse("""{"n":NaN}"""))
        {
            parser.AllowNanInfinity = true;
            using var val = strict.GetField("n");
            await Assert.That(() => val.GetDouble()).Throws<SimdJsonException>();
        }
        using var permissive = parser.Parse("""{"n":NaN}""");
        using var v2 = permissive.GetField("n");
        await Assert.That(double.IsNaN(v2.GetDouble())).IsTrue();
        parser.AllowNanInfinity = false;
        await Assert.That(double.IsNaN(v2.GetDouble())).IsTrue();
    }

    [Test]
    public async Task FiniteNumbers_AreUnaffected()
    {
        using var parser = new SimdJsonParser { AllowNanInfinity = true };
        using var doc = parser.Parse("""{"a":1.25,"b":-3,"c":1e300}""");
        using var a = doc.GetField("a");
        using var b = doc.GetField("b");
        using var c = doc.GetField("c");
        await Assert.That(a.GetDouble()).IsEqualTo(1.25);
        await Assert.That(b.GetInt64()).IsEqualTo(-3L);
        await Assert.That(c.GetDouble()).IsEqualTo(1e300);
    }
}

public class NanInfinityStreamTests
{
    private static readonly byte[] Input = Encoding.UTF8.GetBytes("{\"v\":NaN}\n{\"v\":-Infinity}\n");

    private static double SelectV(JsonDocument doc)
    {
        using var v = doc.GetField("v");
        return v.GetDouble();
    }

    [Test]
    public async Task OpenStream_HonoursOption()
    {
        using var stream = NdjsonParser.OpenStream(Input, new NdjsonParserOptions { AllowNanInfinity = true });
        var seen = new List<double>();
        while (stream.MoveNext()) seen.Add(SelectV(stream.Current!));
        await Assert.That(seen.Count).IsEqualTo(2);
        await Assert.That(double.IsNaN(seen[0])).IsTrue();
        await Assert.That(seen[1]).IsEqualTo(double.NegativeInfinity);
    }

    [Test]
    public async Task OpenStream_IsStrictByDefault()
    {
        using var stream = NdjsonParser.OpenStream(Input);
        await Assert.That(stream.MoveNext()).IsTrue();
        await Assert.That(() => SelectV(stream.Current!)).Throws<SimdJsonException>();
    }

    [Test]
    public async Task ParseAsync_HonoursOption()
    {
        using var ms = new MemoryStream(Input);
        var seen = new List<double>();
        await foreach (var d in NdjsonParser.ParseAsync(ms, SelectV, new NdjsonParserOptions { AllowNanInfinity = true }))
        {
            seen.Add(d);
        }
        await Assert.That(seen.Count).IsEqualTo(2);
        await Assert.That(double.IsNaN(seen[0])).IsTrue();
        await Assert.That(seen[1]).IsEqualTo(double.NegativeInfinity);
    }
}
