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
