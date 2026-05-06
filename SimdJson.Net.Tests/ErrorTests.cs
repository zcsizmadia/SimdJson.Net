using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class ErrorTests
{
    [Test]
    [Arguments(-1,  "Parser capacity exceeded.")]
    [Arguments(-2,  "Incorrect JSON value type.")]
    [Arguments(-3,  "No such field.")]
    [Arguments(-4,  "Index out of bounds.")]
    [Arguments(-5,  "Null pointer passed to native bridge.")]
    [Arguments(-6,  "JSON parse error.")]
    [Arguments(-7,  "Iteration error.")]
    [Arguments(-99, "Unknown native error.")]
    [Arguments(-42, "Native error -42.")]
    public async Task SimdJsonException_MessageMatchesErrorCode(int code, string expectedMessage)
    {
        var ex = new SimdJsonException(code);
        await Assert.That(ex.Message).IsEqualTo(expectedMessage);
        await Assert.That(ex.ErrorCode).IsEqualTo(code);
    }

    [Test]
    public async Task GetField_WrongType_ThrowsIncorrectType()
    {
        // accessing field on a non-object value surfaces INCORRECT_TYPE
        using var doc = SimdJsonParser.Shared.Parse("""{"n":42}""");
        using var val = doc.GetField("n");  // a number
        var ex = await Assert.That(() => val.GetString()).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-2);
    }

    [Test]
    public async Task GetField_MissingKey_ThrowsNoSuchField()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var ex = await Assert.That(() => { using var v = doc.GetField("z"); })
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-3);
    }

    [Test]
    public async Task Parse_MalformedJson_ThrowsOnAccess()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"unclosed":""");
        var ex = await Assert.That(() => doc.GetField("unclosed"))
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-6);
    }
}

// ─── Number type tests ───────────────────────────────────────────────────────

public class DeepNestingTests
{
    [Test]
    public async Task ThreeLevelNesting_AccessesCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":{"c":42}}}""");
        using var a = doc.GetField("a");
        using var b = a.GetField("b");
        using var c = b.GetField("c");
        await Assert.That(c.GetInt64()).IsEqualTo(42L);
    }

    [Test]
    public async Task ThreeLevelNesting_ViaIndexer()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":{"c":"deep"}}}""");
        using var val = doc["a"]["b"]["c"];
        await Assert.That(val.GetString()).IsEqualTo("deep");
    }

    [Test]
    public async Task AtPointer_ThreeLevels_Works()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":{"y":{"z":99}}}""");
        using var val = doc.AtPointer("/x/y/z");
        await Assert.That(val.GetInt64()).IsEqualTo(99L);
    }

    [Test]
    public async Task ObjectOfArrays_CanBeRead()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"evens":[2,4,6],"odds":[1,3,5]}""");
        using var evensVal = doc.GetField("evens");
        using var evens = evensVal.GetArray();
        var even = new List<long>();
        foreach (var n in evens)
        {
            even.Add(n.GetInt64());
        }

        using var oddsVal = doc.GetField("odds");
        using var odds = oddsVal.GetArray();
        var odd = new List<long>();
        foreach (var n in odds)
        {
            odd.Add(n.GetInt64());
        }

        await Assert.That(even).IsEquivalentTo(new[] { 2L, 4L, 6L });
        await Assert.That(odd).IsEquivalentTo(new[] { 1L, 3L, 5L });
    }

    [Test]
    public async Task ArrayOfObjects_AtPointer_Works()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[{"v":10},{"v":20},{"v":30}]""");
        using var second = doc.AtPointer("/1/v");
        await Assert.That(second.GetInt64()).IsEqualTo(20L);
    }
}

public class ParseAllowIncompleteJsonTests
{
    [Test]
    public async Task ParseAllowIncompleteJson_CompleteJson_ParsesNormally()
    {
        using var doc = SimdJsonParser.Shared.ParseAllowIncompleteJson("""{"key":"value"}""");
        using var val = doc.GetField("key");
        await Assert.That(val.GetString()).IsEqualTo("value");
    }

    [Test]
    public async Task ParseAllowIncompleteJson_TruncatedArray_DoesNotThrowOnParse()
    {
        // Truncated JSON — parse itself should not throw (access may or may not fail)
        var truncated = "[1,2,3"u8.ToArray();
        await Assert.That(() =>
        {
            using var doc = SimdJsonParser.Shared.ParseAllowIncompleteJson(truncated);
        }).ThrowsNothing();
    }

    [Test]
    public async Task ParseAllowIncompleteJson_StringOverload_CompleteJson_Works()
    {
        using var doc = SimdJsonParser.Shared.ParseAllowIncompleteJson("""[1,2,3]""");
        using var arr = doc.GetArray();
        await Assert.That(arr.Count).IsEqualTo(3);
    }
}
