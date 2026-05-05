using System.Text;

namespace SimdJson.Net.Tests;

public class BasicParseTests
{
    [Test]
    public async Task Parse_SimpleObject()
    {
        using var doc = JsonDocument.Parse("""{"name":"Ada","age":36}""");
        var root = doc.Root;
        await Assert.That(root.ValueKind).IsEqualTo(JsonElementType.Object);
        await Assert.That(root.GetLength()).IsEqualTo(2);
        await Assert.That(root["name"].GetString()).IsEqualTo("Ada");
        await Assert.That(root["age"].GetInt64()).IsEqualTo(36L);
    }

    [Test]
    public async Task Parse_Array_Of_Numbers()
    {
        using var doc = JsonDocument.Parse("[1, 2, 3, 4, 5]");
        var root = doc.Root;
        await Assert.That(root.ValueKind).IsEqualTo(JsonElementType.Array);
        await Assert.That(root.GetLength()).IsEqualTo(5);
        long sum = 0;
        foreach (var el in root.EnumerateArray()) sum += el.GetInt64();
        await Assert.That(sum).IsEqualTo(15L);
    }

    [Test]
    public async Task Parse_Booleans_And_Null()
    {
        using var doc = JsonDocument.Parse("""{"a":true,"b":false,"c":null}""");
        var r = doc.Root;
        await Assert.That(r["a"].GetBoolean()).IsTrue();
        await Assert.That(r["b"].GetBoolean()).IsFalse();
        await Assert.That(r["c"].ValueKind).IsEqualTo(JsonElementType.Null);
    }

    [Test]
    public async Task Parse_Doubles()
    {
        using var doc = JsonDocument.Parse("[1.5, -2.25, 1e3]");
        var arr = doc.Root;
        await Assert.That(arr[0].GetDouble()).IsEqualTo(1.5);
        await Assert.That(arr[1].GetDouble()).IsEqualTo(-2.25);
        await Assert.That(arr[2].GetDouble()).IsEqualTo(1000.0);
    }

    [Test]
    public async Task Parse_Nested_Object()
    {
        using var doc = JsonDocument.Parse("""{"outer":{"inner":[1,{"x":"y"}]}}""");
        var inner = doc.Root["outer"]["inner"];
        await Assert.That(inner.GetLength()).IsEqualTo(2);
        await Assert.That(inner[0].GetInt64()).IsEqualTo(1L);
        await Assert.That(inner[1]["x"].GetString()).IsEqualTo("y");
    }

    [Test]
    public async Task Parse_String_With_Escapes()
    {
        using var doc = JsonDocument.Parse("""{"k":"a\"b\\c\n"}""");
        await Assert.That(doc.Root["k"].GetString()).IsEqualTo("a\"b\\c\n");
    }

    [Test]
    public async Task Parse_String_With_Unicode_Escape()
    {
        using var doc = JsonDocument.Parse("""{"k":"\u00e9"}""");
        await Assert.That(doc.Root["k"].GetString()).IsEqualTo("é");
    }

    [Test]
    public async Task Parse_Empty_Containers()
    {
        using var doc = JsonDocument.Parse("""{"a":[],"b":{}}""");
        await Assert.That(doc.Root["a"].GetLength()).IsEqualTo(0);
        await Assert.That(doc.Root["b"].GetLength()).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_Large_Array()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 1000; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(i);
        }
        sb.Append(']');
        using var doc = JsonDocument.Parse(sb.ToString());
        await Assert.That(doc.Root.GetLength()).IsEqualTo(1000);
        await Assert.That(doc.Root[999].GetInt64()).IsEqualTo(999L);
    }

    [Test]
    public async Task Throws_On_Truncated()
    {
        await Assert.That(() => { _ = JsonDocument.Parse("{"); }).Throws<SimdJsonException>();
    }

    [Test]
    public async Task Throws_On_Garbage()
    {
        await Assert.That(() => { _ = JsonDocument.Parse("{not json}"); }).Throws<SimdJsonException>();
    }
}
