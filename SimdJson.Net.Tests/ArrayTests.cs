using System.Collections;
using System.Text;

namespace SimdJson.Tests;

public class ArrayTests
{
    [Test]
    public async Task Enumerate_Array_YieldsAllElements()
    {
        using var doc = SimdJsonParser.Shared.Parse("[10,20,30]");
        using var arr = doc.GetArray();
        var values = new List<long>();
        foreach (var item in arr)
        {
            values.Add(item.GetInt64());
        }

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
        foreach (var _ in arr)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Array_MixedTypes_ValueKinds()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1, "hello", true, null, 3.14]""");
        using var arr = doc.GetArray();
        var kinds = new List<JsonValueKind>();
        foreach (var item in arr)
        {
            kinds.Add(item.ValueKind);
        }

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
            {
                all.Add(cell.GetInt64());
            }
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
        {
            values.Add(item.GetInt64());
        }

        await Assert.That(values).IsEquivalentTo(new[] { 7L, 8L, 9L });
    }
}

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
