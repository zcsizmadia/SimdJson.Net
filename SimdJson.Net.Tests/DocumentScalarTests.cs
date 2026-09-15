namespace SimdJson.Tests;

// ─── JsonDocument 32-bit scalar getters ───────────────────────────────────────

public class DocumentInt32Tests
{
    [Test]
    [Arguments("0", 0)]
    [Arguments("42", 42)]
    [Arguments("-42", -42)]
    [Arguments("2147483647", int.MaxValue)]
    [Arguments("-2147483648", int.MinValue)]
    public async Task GetInt32_ScalarRoot_ReturnsValue(string json, int expected)
    {
        using var doc = SimdJsonParser.Shared.Parse(json);
        await Assert.That(doc.GetInt32()).IsEqualTo(expected);
    }

    [Test]
    [Arguments("0", 0u)]
    [Arguments("42", 42u)]
    [Arguments("4294967295", uint.MaxValue)]
    public async Task GetUInt32_ScalarRoot_ReturnsValue(string json, uint expected)
    {
        using var doc = SimdJsonParser.Shared.Parse(json);
        await Assert.That(doc.GetUInt32()).IsEqualTo(expected);
    }

    [Test]
    [Arguments("2147483648")]
    [Arguments("-2147483649")]
    [Arguments("5000000000")]
    public async Task GetInt32_OutOfRange_ThrowsNumberOutOfRange(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse(json);
        var ex = await Assert.That(() => doc.GetInt32()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-10);
    }

    [Test]
    [Arguments("4294967296")]
    [Arguments("-1")]
    public async Task GetUInt32_OutOfRange_Throws(string json)
    {
        using var doc = SimdJsonParser.Shared.Parse(json);
        await Assert.That(() => doc.GetUInt32()).Throws<SimdJsonException>();
    }

    [Test]
    public async Task GetInt32_NotANumber_ThrowsIncorrectType()
    {
        using var doc = SimdJsonParser.Shared.Parse("\"text\"");
        var ex = await Assert.That(() => doc.GetInt32()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-2);
    }

    [Test]
    public async Task GetInt32_MatchesValueLevelGetter()
    {
        using var docParser = new SimdJsonParser();
        using var doc = docParser.Parse("123");
        using var valueParser = new SimdJsonParser();
        using var wrapped = valueParser.Parse("""{"n":123}""");
        using var val = wrapped.GetField("n");
        await Assert.That(doc.GetInt32()).IsEqualTo(val.GetInt32());
    }

    [Test]
    public async Task GetInt32_AfterDispose_Throws()
    {
        var parser = new SimdJsonParser();
        var doc = parser.Parse("1");
        doc.Dispose();
        await Assert.That(() => doc.GetInt32()).Throws<ObjectDisposedException>();
        parser.Dispose();
    }
}

// ─── JsonDocument.AtEnd ───────────────────────────────────────────────────────

public class DocumentAtEndTests
{
    [Test]
    public async Task AtEnd_CleanArrayRoot_IsTrueAfterConsuming()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""[1,2,3]""");
        using (var arr = doc.GetArray())
        {
            foreach (var item in arr)
            {
                item.Dispose();
            }
        }

        await Assert.That(doc.AtEnd()).IsTrue();
    }

    [Test]
    public async Task AtEnd_TrailingContentAfterArray_IsFalse()
    {
        // The trailing text must itself be well-formed JSON. A bare word is rejected as a
        // malformed atom during parsing, which is a different failure mode.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""[1,2] [3,4]""");
        using (var arr = doc.GetArray())
        {
            foreach (var item in arr)
            {
                item.Dispose();
            }
        }

        await Assert.That(doc.AtEnd()).IsFalse();
    }

    [Test]
    public async Task AtEnd_CleanObjectRoot_IsTrueAfterConsuming()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a":1}""");
        using (var obj = doc.GetObject())
        {
            foreach (var prop in obj)
            {
                prop.Value.Dispose();
            }
        }

        await Assert.That(doc.AtEnd()).IsTrue();
    }

    [Test]
    public async Task AtEnd_TrailingContentAfterObject_IsFalse()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a":1} {"b":2}""");
        using (var obj = doc.GetObject())
        {
            foreach (var prop in obj)
            {
                prop.Value.Dispose();
            }
        }

        await Assert.That(doc.AtEnd()).IsFalse();
    }

    [Test]
    public async Task AtEnd_AfterDispose_Throws()
    {
        var parser = new SimdJsonParser();
        var doc = parser.Parse("""[1]""");
        doc.Dispose();
        await Assert.That(() => doc.AtEnd()).Throws<ObjectDisposedException>();
        parser.Dispose();
    }
}

// ─── Parser.Allocate / MaxDepth ───────────────────────────────────────────────

public class ParserAllocateTests
{
    [Test]
    public async Task Allocate_SetsMaxDepth()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 32);
        await Assert.That(parser.MaxDepth).IsEqualTo((nuint)32);
    }

    [Test]
    public async Task Allocate_SetsCapacity()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024);
        await Assert.That(parser.Capacity).IsEqualTo((nuint)(64 * 1024));
    }

    [Test]
    public async Task Allocate_ZeroDepth_ThrowsArgumentOutOfRange()
    {
        using var parser = new SimdJsonParser();
        await Assert.That(() => parser.Allocate(1024, 0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Allocate_ThenParse_Works()
    {
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 64);
        using var doc = parser.Parse("""{"a":[1,2,3]}""");
        using var arr = doc.GetField("a").GetArray();
        await Assert.That(arr.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Allocate_LowDepth_LimitsWildcardIteration()
    {
        // simdjson consults max_depth when walking JSONPath wildcards and reports -12
        // rather than recursing past the limit.
        using var parser = new SimdJsonParser();
        parser.Allocate(64 * 1024, 1);
        using var doc = parser.Parse("""{"a":{"b":[1,2,3]}}""");
        var ex = await Assert.That(() => doc.ForEachAtPath("$.a.b[*]", _ => { }))
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-12);
    }

    [Test]
    public async Task Allocate_AfterDispose_Throws()
    {
        var parser = new SimdJsonParser();
        parser.Dispose();
        await Assert.That(() => parser.Allocate(1024)).Throws<ObjectDisposedException>();
    }
}
