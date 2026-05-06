using System.Collections;
using System.Text;

namespace SimdJson.Tests;

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
        foreach (var _ in obj)
        {
            count++;
        }

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
// ─── Object extra coverage tests ──────────────────────────────────────────────

public class ObjectExtraTests
{
    [Test]
    public async Task TryGetField_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        var ok = obj.TryGetField("missing", out var v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsNull();
    }

    [Test]
    public async Task ContainsKey_MissingKey_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1}""");
        using var obj = doc.GetObject();
        await Assert.That(obj.ContainsKey("missing")).IsFalse();
    }

    [Test]
    public async Task ObjectAtPointer_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":42}}""");
        using var obj = doc.GetObject();
        using var val = obj.AtPointer("/a/b");
        await Assert.That(val.GetInt64()).IsEqualTo(42L);
    }

    [Test]
    public async Task ObjectAtPath_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":42}}""");
        using var obj = doc.GetObject();
        using var val = obj.AtPath("$.a.b");
        await Assert.That(val.GetInt64()).IsEqualTo(42L);
    }

    [Test]
    public async Task ObjectTryAtPointer_ValidPath_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":{"b":7}}""");
        using var obj = doc.GetObject();
        var ok = obj.TryAtPointer("/a/b", out var v);
        using var val = v!;
        await Assert.That(ok).IsTrue();
        await Assert.That(val.GetInt64()).IsEqualTo(7L);
    }

    [Test]
    public async Task ObjectTryAtPath_ValidPath_ReturnsTrue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":99}""");
        using var obj = doc.GetObject();
        var ok = obj.TryAtPath("$.x", out var v);
        using var val = v!;
        await Assert.That(ok).IsTrue();
        await Assert.That(val.GetInt64()).IsEqualTo(99L);
    }

    [Test]
    public async Task ObjectTryAtPath_InvalidPath_ReturnsFalse()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"x":99}""");
        using var obj = doc.GetObject();
        var ok = obj.TryAtPath("$.missing", out var v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsNull();
    }

    [Test]
    public async Task ObjectFindField_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"score":8.5}""");
        using var obj = doc.GetObject();
        using var val = obj.FindField("score");
        await Assert.That(val.GetDouble()).IsEqualTo(8.5);
    }

    [Test]
    public async Task ObjectFindFieldUnordered_ReturnsCorrectValue()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"c":3,"b":2,"a":1}""");
        using var obj = doc.GetObject();
        using var val = obj.FindFieldUnordered("a");
        await Assert.That(val.GetInt64()).IsEqualTo(1L);
    }
}