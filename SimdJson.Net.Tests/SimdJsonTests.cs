using SimdJson;
using System.Collections;
using System.Text;
using TUnit.Assertions.Extensions;

namespace SimdJson.Tests;

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

public class ArrayTests
{
    [Test]
    public async Task Enumerate_Array_YieldsAllElements()
    {
        using var doc = SimdJsonParser.Shared.Parse("[10,20,30]");
        using var arr = doc.GetArray();
        var values = new List<long>();
        foreach (var item in arr)
            values.Add(item.GetInt64());
        await Assert.That(values).IsEquivalentTo(new[] { 10L, 20L, 30L });
    }

    [Test]
    public async Task Count_ReturnsCorrectCount()
    {
        using var doc = SimdJsonParser.Shared.Parse("[1,2,3,4,5]");
        using var arr = doc.GetArray();
        await Assert.That(arr.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Array_NestedObjects_CanBeRead()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[{"id":1},{"id":2}]""");
        using var arr = doc.GetArray();
        var ids = new List<long>();
        foreach (var item in arr)
        {
            using var obj = item.GetObject();
            using var id = obj.GetField("id");
            ids.Add(id.GetInt64());
        }
        await Assert.That(ids).IsEquivalentTo(new[] { 1L, 2L });
    }

    [Test]
    public async Task Array_Empty_CountIsZero()
    {
        using var doc = SimdJsonParser.Shared.Parse("[]");
        using var arr = doc.GetArray();
        await Assert.That(arr.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Array_Empty_EnumeratesZeroItems()
    {
        using var doc = SimdJsonParser.Shared.Parse("[]");
        using var arr = doc.GetArray();
        var count = 0;
        foreach (var _ in arr) count++;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Array_MixedTypes_ValueKinds()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1, "hello", true, null, 3.14]""");
        using var arr = doc.GetArray();
        var kinds = new List<JsonValueKind>();
        foreach (var item in arr)
            kinds.Add(item.ValueKind);

        await Assert.That(kinds[0]).IsEqualTo(JsonValueKind.Number);
        await Assert.That(kinds[1]).IsEqualTo(JsonValueKind.String);
        await Assert.That(kinds[2]).IsEqualTo(JsonValueKind.Boolean);
        await Assert.That(kinds[3]).IsEqualTo(JsonValueKind.Null);
        await Assert.That(kinds[4]).IsEqualTo(JsonValueKind.Number);
    }

    [Test]
    public async Task Array_NestedArrays_CanBeRead()
    {
        using var doc = SimdJsonParser.Shared.Parse("[[1,2],[3,4]]");
        using var outer = doc.GetArray();
        var all = new List<long>();
        foreach (var row in outer)
        {
            using var inner = row.GetArray();
            foreach (var cell in inner)
                all.Add(cell.GetInt64());
        }
        await Assert.That(all).IsEquivalentTo(new[] { 1L, 2L, 3L, 4L });
    }

    [Test]
    public async Task Array_ExplicitIEnumerable_Works()
    {
        using var doc = SimdJsonParser.Shared.Parse("[7,8,9]");
        using var arr = doc.GetArray();
        // exercises the explicit IEnumerable.GetEnumerator() path
        IEnumerable nonGeneric = arr;
        var values = new List<long>();
        foreach (JsonValue item in nonGeneric)
            values.Add(item.GetInt64());
        await Assert.That(values).IsEquivalentTo(new[] { 7L, 8L, 9L });
    }
}

public class ObjectTests
{
    [Test]
    public async Task Enumerate_Object_YieldsAllProperties()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        using var obj = doc.GetObject();
        var props = new Dictionary<string, long>();
        foreach (var prop in obj)
        {
            props[prop.Name] = prop.Value.GetInt64();
            prop.Value.Dispose();
        }
        await Assert.That(props.Count).IsEqualTo(3);
        await Assert.That(props["a"]).IsEqualTo(1L);
        await Assert.That(props["b"]).IsEqualTo(2L);
        await Assert.That(props["c"]).IsEqualTo(3L);
    }

    [Test]
    public async Task Count_ReturnsCorrectCount()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1,"y":2}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetField_Indexer_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"key":"value"}""");
        using var obj = doc.GetObject();
        using var val = obj["key"];
        await Assert.That(val.GetString()).IsEqualTo("value");
    }

    [Test]
    public async Task Object_Empty_CountIsZero()
    {
        using var doc = SimdJsonParser.Shared.Parse("{}");
        using var obj = doc.GetObject();
        await Assert.That(obj.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Object_Empty_EnumeratesZeroProperties()
    {
        using var doc = SimdJsonParser.Shared.Parse("{}");
        using var obj = doc.GetObject();
        var count = 0;
        foreach (var _ in obj) count++;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Object_ExplicitIEnumerable_Works()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"p":1,"q":2}""");
        using var obj = doc.GetObject();
        // exercises the explicit IEnumerable.GetEnumerator() path
        IEnumerable nonGeneric = obj;
        var names = new List<string>();
        foreach (JsonProperty prop in nonGeneric)
        {
            names.Add(prop.Name);
            prop.Value.Dispose();
        }
        await Assert.That(names).IsEquivalentTo(new[] { "p", "q" });
    }
}

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
        foreach (var n in evens) even.Add(n.GetInt64());

        using var oddsVal = doc.GetField("odds");
        using var odds = oddsVal.GetArray();
        var odd = new List<long>();
        foreach (var n in odds) odd.Add(n.GetInt64());

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

public class UnicodeTests
{
    [Test]
    public async Task Unicode_Emoji_RoundTrips()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"e":"🎉"}""");
        using var val = doc.GetField("e");
        await Assert.That(val.GetString()).IsEqualTo("🎉");
    }

    [Test]
    public async Task Unicode_CJK_RoundTrips()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"t":"汉字"}""");
        using var val = doc.GetField("t");
        await Assert.That(val.GetString()).IsEqualTo("汉字");
    }

    [Test]
    public async Task Unicode_EscapeSequences_AreUnescaped()
    {
        // \u0041 = 'A', \u0042 = 'B'
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"\u0041\u0042"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetString()).IsEqualTo("AB");
    }

    [Test]
    public async Task Unicode_EscapedNewline_InString()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"s":"line1\nline2"}""");
        using var val = doc.GetField("s");
        await Assert.That(val.GetString()).IsEqualTo("line1\nline2");
    }
}

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

public class RawJsonTests
{
    [Test]
    public async Task GetRawJsonToken_Number_ReturnsDigits()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":42}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("42");
    }

    [Test]
    public async Task GetRawJsonToken_String_IncludesQuotes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":"hello"}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task GetRawJsonToken_Bool_ReturnsWord()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":true}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("true");
    }

    [Test]
    public async Task GetRawJsonToken_Null_ReturnsNull()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":null}""");
        using var val = doc.GetField("v");
        await Assert.That(val.GetRawJsonToken()).IsEqualTo("null");
    }

    [Test]
    public async Task GetRawJsonTokenSpan_Number_ReturnsCorrectBytes()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"v":99}""");
        using var val = doc.GetField("v");
        var span = val.GetRawJsonTokenSpan();
        await Assert.That(System.Text.Encoding.UTF8.GetString(span)).IsEqualTo("99");
    }

    [Test]
    public async Task GetRawJson_Object_ReturnsFullJson()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"obj":{"a":1,"b":2}}""");
        using var val = doc.GetField("obj");
        var raw = val.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"a":1,"b":2}""");
    }

    [Test]
    public async Task GetRawJson_Array_ReturnsFullJson()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"arr":[1,2,3]}""");
        using var val = doc.GetField("arr");
        var raw = val.GetRawJson();
        await Assert.That(raw).IsEqualTo("[1,2,3]");
    }

    [Test]
    public async Task ArrayGetRawJson_ReturnsFullArray()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[10,20,30]""");
        using var arr = doc.GetArray();
        var raw = arr.GetRawJson();
        await Assert.That(raw).IsEqualTo("[10,20,30]");
    }

    [Test]
    public async Task ObjectGetRawJson_ReturnsFullObject()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1,"y":2}""");
        using var obj = doc.GetObject();
        var raw = obj.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"x":1,"y":2}""");
    }

    [Test]
    public async Task DocumentGetRawJson_ReturnsFullDocument()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var raw = doc.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"a":1}""");
    }
}

// ─── Numbers in strings tests ─────────────────────────────────────────────────

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

public class AtPathTests
{
    [Test]
    public async Task DocumentAtPath_SimpleKey_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice","age":30}""");
        using var val = doc.AtPath("$.name");
        await Assert.That(val.GetString()).IsEqualTo("Alice");
    }

    [Test]
    public async Task DocumentAtPath_NestedObject_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"user":{"id":1,"name":"Bob"}}""");
        using var val = doc.AtPath("$.user.name");
        await Assert.That(val.GetString()).IsEqualTo("Bob");
    }

    [Test]
    public async Task DocumentAtPath_ArrayIndex_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"items":[10,20,30]}""");
        using var val = doc.AtPath("$.items[1]");
        await Assert.That(val.GetInt64()).IsEqualTo(20L);
    }

    [Test]
    public async Task DocumentTryAtPath_MissingPath_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        await Assert.That(doc.TryAtPath("$.missing", out var v)).IsFalse();
        await Assert.That(v).IsNull();
    }

    [Test]
    public async Task ObjectAtPath_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"meta":{"version":"1.0"}}""");
        using var obj = doc.GetObject();
        using var val = obj.AtPath("$.meta.version");
        await Assert.That(val.GetString()).IsEqualTo("1.0");
    }

    [Test]
    public async Task ValueAtPath_ReturnsDescendant()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"data":{"nested":{"value":99}}}""");
        using var data = doc.GetField("data");
        using var val = data.AtPath("$.nested.value");
        await Assert.That(val.GetInt64()).IsEqualTo(99L);
    }
}

// ─── AtPointer on Value/Object tests ─────────────────────────────────────────

public class AtPointerExtendedTests
{
    [Test]
    public async Task ValueAtPointer_DeepPath_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":{"c":42}}}""");
        using var root = doc.GetField("a");
        using var val = root.AtPointer("/b/c");
        await Assert.That(val.GetInt64()).IsEqualTo(42L);
    }

    [Test]
    public async Task ValueTryAtPointer_InvalidPath_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var root = doc.GetField("a");
        // 'a' is a number, can't navigate into it
        await Assert.That(root.TryAtPointer("/b", out _)).IsFalse();
    }

    [Test]
    public async Task ObjectAtPointer_DeepPath_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"level1":{"level2":{"val":"deep"}}}""");
        using var obj = doc.GetObject();
        using var val = obj.AtPointer("/level1/level2/val");
        await Assert.That(val.GetString()).IsEqualTo("deep");
    }

    [Test]
    public async Task ObjectTryAtPointer_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":1}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.TryAtPointer("/missing", out _)).IsFalse();
    }
}

// ─── FindField tests ──────────────────────────────────────────────────────────

public class FindFieldTests
{
    [Test]
    public async Task DocumentFindField_FirstField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        using var val = doc.FindField("a");
        await Assert.That(val.GetInt64()).IsEqualTo(1L);
    }

    [Test]
    public async Task DocumentFindField_SecondField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        using var val = doc.FindField("b");
        await Assert.That(val.GetInt64()).IsEqualTo(2L);
    }

    [Test]
    public async Task ObjectFindField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"name":"Charlie","score":100}""");
        using var obj = doc.GetObject();
        using var val = obj.FindField("score");
        await Assert.That(val.GetInt64()).IsEqualTo(100L);
    }

    [Test]
    public async Task ValueFindField_OnObjectValue_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"config":{"enabled":true,"timeout":30}}""");
        using var config = doc.GetField("config");
        using var val = config.FindField("timeout");
        await Assert.That(val.GetInt64()).IsEqualTo(30L);
    }

    [Test]
    public async Task FindField_MissingKey_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var ex = await Assert.That(() => { using var v = doc.FindField("z"); })
            .Throws<SimdJsonException>();
        await Assert.That(ex).IsNotNull();
    }
}

// ─── Document Rewind tests ────────────────────────────────────────────────────

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

public class ArrayAtTests
{
    [Test]
    public async Task At_FirstElement_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[100,200,300]""");
        using var arr = doc.GetArray();
        using var val = arr.At(0);
        await Assert.That(val.GetInt64()).IsEqualTo(100L);
    }

    [Test]
    public async Task At_LastElement_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[100,200,300]""");
        using var arr = doc.GetArray();
        using var val = arr.At(2);
        await Assert.That(val.GetInt64()).IsEqualTo(300L);
    }

    [Test]
    public async Task At_MiddleElement_MatchesElementAt()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[10,20,30,40,50]""");
        using var arr = doc.GetArray();
        using var v = arr.At(3);
        await Assert.That(v.GetInt64()).IsEqualTo(40L);
    }

    [Test]
    public async Task At_OutOfBounds_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        using var arr = doc.GetArray();
        var ex = await Assert.That(() => { using var v = arr.At(10); })
            .Throws<SimdJsonException>();
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task ArrayReset_AfterGetRawJson_AllowsIteration()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        using var arr = doc.GetArray();
        var raw = arr.GetRawJson();
        await Assert.That(raw).IsEqualTo("[1,2,3]");

        arr.Reset();
        int sum = 0;
        foreach (var v in arr)
        {
            sum += (int)v.GetInt64();
            v.Dispose();
        }
        await Assert.That(sum).IsEqualTo(6);
    }
}

// ─── Minify tests ─────────────────────────────────────────────────────────────

public class MinifyTests
{
    [Test]
    public async Task Minify_RemovesWhitespace()
    {
        var result = SimdJsonParser.Minify("""{ "a" : 1, "b" : 2 }""");
        await Assert.That(result).IsEqualTo("""{"a":1,"b":2}""");
    }

    [Test]
    public async Task Minify_AlreadyMinified_ReturnsSame()
    {
        var json = """{"x":1}""";
        var result = SimdJsonParser.Minify(json);
        await Assert.That(result).IsEqualTo(json);
    }

    [Test]
    public async Task Minify_Nested_RemovesAllWhitespace()
    {
        var result = SimdJsonParser.Minify("{\n  \"arr\": [\n    1,\n    2,\n    3\n  ]\n}");
        await Assert.That(result).IsEqualTo("""{"arr":[1,2,3]}""");
    }

    [Test]
    public async Task MinifyUtf8_ProducesCorrectBytes()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("""{ "k" : "v" }""");
        var result = SimdJsonParser.MinifyUtf8(input);
        var str = System.Text.Encoding.UTF8.GetString(result);
        await Assert.That(str).IsEqualTo("""{"k":"v"}""");
    }

    [Test]
    public async Task Minify_PreservesStringsWithSpaces()
    {
        var result = SimdJsonParser.Minify("""{ "msg" : "hello world" }""");
        await Assert.That(result).IsEqualTo("""{"msg":"hello world"}""");
    }
}

// ─── UTF-8 validation tests ───────────────────────────────────────────────────

public class Utf8ValidationTests
{
    [Test]
    public async Task ValidateUtf8_AsciiBytes_ReturnsTrue()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("Hello, world!");
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_ValidMultibyteBytes_ReturnsTrue()
    {
        // "こんにちは" is valid UTF-8
        var bytes = System.Text.Encoding.UTF8.GetBytes("こんにちは");
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_InvalidBytes_ReturnsFalse()
    {
        // 0xFF is not valid in any UTF-8 sequence
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0xFF };
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsFalse();
    }

    [Test]
    public async Task ValidateUtf8_EmptySpan_ReturnsTrue()
    {
        await Assert.That(SimdJsonParser.ValidateUtf8(ReadOnlySpan<byte>.Empty)).IsTrue();
    }

    [Test]
    public async Task ValidateUtf8_OverlongEncoding_ReturnsFalse()
    {
        // Overlong encoding of U+0000 (2-byte): 0xC0, 0x80
        var bytes = new byte[] { 0xC0, 0x80 };
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsFalse();
    }

    [Test]
    public async Task ValidateUtf8_ValidEmojiBytes_ReturnsTrue()
    {
        // 😀 = U+1F600 encoded as 4-byte UTF-8: F0 9F 98 80
        var bytes = new byte[] { 0xF0, 0x9F, 0x98, 0x80 };
        await Assert.That(SimdJsonParser.ValidateUtf8(bytes)).IsTrue();
    }
}

// ─── Object Reset tests ───────────────────────────────────────────────────────

public class ObjectResetTests
{
    [Test]
    public async Task ObjectReset_AfterGetRawJson_AllowsIteration()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2}""");
        using var obj = doc.GetObject();
        var raw = obj.GetRawJson();
        await Assert.That(raw).IsEqualTo("""{"a":1,"b":2}""");

        obj.Reset();
        int count = 0;
        foreach (var prop in obj)
        {
            count++;
            prop.Value.Dispose();
        }
        await Assert.That(count).IsEqualTo(2);
    }
}

// ─── IsScalar / IsString tests ────────────────────────────────────────────────

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

// ─── Parser configuration tests ──────────────────────────────────────────────

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

// ─── Array AtPointer / AtPath tests ──────────────────────────────────────────

public class ArrayPointerPathTests
{
    [Test]
    public async Task ArrayAtPointer_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[{"name":"Alice"},{"name":"Bob"}]""");
        using var arr = doc.GetArray();
        using var val = arr.AtPointer("/1/name");
        await Assert.That(val.GetString()).IsEqualTo("Bob");
    }

    [Test]
    public async Task ArrayTryAtPointer_InvalidPath_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        using var arr = doc.GetArray();
        var result = arr.TryAtPointer("/99/x", out var v);
        await Assert.That(result).IsFalse();
        await Assert.That(v).IsNull();
    }

    [Test]
    public async Task ArrayAtPath_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[{"name":"Alice"},{"name":"Bob"}]""");
        using var arr = doc.GetArray();
        using var val = arr.AtPath("$[0].name");
        await Assert.That(val.GetString()).IsEqualTo("Alice");
    }
}

// ─── New gap-filling tests ────────────────────────────────────────────────────

public class ObjectIsEmptyTests
{
    [Test]
    public async Task ObjectIsEmpty_EmptyObject_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("{}");
        using var obj = doc.GetObject();
        await Assert.That(obj.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task ObjectIsEmpty_NonEmptyObject_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.IsEmpty()).IsFalse();
    }
}

public class FindFieldUnorderedTests
{
    [Test]
    public async Task ObjectFindFieldUnordered_LastField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        using var obj = doc.GetObject();
        using var val = obj.FindFieldUnordered("c");
        await Assert.That(val.GetInt64()).IsEqualTo(3L);
    }

    [Test]
    public async Task ObjectFindFieldUnordered_MissingKey_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        await Assert.That(() => obj.FindFieldUnordered("z"))
            .Throws<SimdJsonException>();
    }

    [Test]
    public async Task ObjectTryFindFieldUnordered_ExistingKey_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":42}""");
        using var obj = doc.GetObject();
        var found = obj.TryFindFieldUnordered("x", out var val);
        using var _ = val;
        await Assert.That(found).IsTrue();
        await Assert.That(val!.GetInt64()).IsEqualTo(42L);
    }

    [Test]
    public async Task ObjectTryFindFieldUnordered_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":42}""");
        using var obj = doc.GetObject();
        var found = obj.TryFindFieldUnordered("z", out var val);
        await Assert.That(found).IsFalse();
        await Assert.That(val).IsNull();
    }

    [Test]
    public async Task DocumentFindFieldUnordered_LastField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"z":99}""");
        using var val = doc.FindFieldUnordered("z");
        await Assert.That(val.GetInt64()).IsEqualTo(99L);
    }

    [Test]
    public async Task DocumentTryFindFieldUnordered_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        var found = doc.TryFindFieldUnordered("missing", out var val);
        await Assert.That(found).IsFalse();
        await Assert.That(val).IsNull();
    }

    [Test]
    public async Task ValueFindFieldUnordered_NestedLastField_ReturnsValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"outer":{"x":1,"y":2}}""");
        using var outer = doc.GetField("outer");
        using var val = outer.FindFieldUnordered("y");
        await Assert.That(val.GetInt64()).IsEqualTo(2L);
    }

    [Test]
    public async Task ValueTryFindFieldUnordered_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"outer":{"x":1}}""");
        using var outer = doc.GetField("outer");
        var found = outer.TryFindFieldUnordered("nope", out var val);
        await Assert.That(found).IsFalse();
        await Assert.That(val).IsNull();
    }
}

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
