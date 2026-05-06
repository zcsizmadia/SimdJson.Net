using System.Collections;
using System.Text;

namespace SimdJson.Tests;

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

public class ForEachAtPathTests
{
    [Test]
    public async Task DocumentForEachAtPath_ArrayWildcard_VisitsAllElements()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        var results = new List<long>();
        doc.ForEachAtPath("$[*]", v => results.Add(v.GetInt64()));
        await Assert.That(results).IsEquivalentTo(new List<long> { 1, 2, 3 });
    }

    [Test]
    public async Task DocumentForEachAtPath_NestedWildcard_VisitsMatchingValues()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"items":[{"x":10},{"x":20},{"x":30}]}""");
        var results = new List<long>();
        doc.ForEachAtPath("$.items[*].x", v => results.Add(v.GetInt64()));
        await Assert.That(results).IsEquivalentTo(new List<long> { 10, 20, 30 });
    }

    [Test]
    public async Task DocumentForEachAtPath_EmptyArray_CallbackNotInvoked()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[]""");
        int count = 0;
        doc.ForEachAtPath("$[*]", _ => count++);
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ValueForEachAtPath_ArrayWildcard_VisitsAllElements()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"arr":[4,5,6]}""");
        using var arrVal = doc.GetField("arr");
        var results = new List<long>();
        arrVal.ForEachAtPath("$[*]", v => results.Add(v.GetInt64()));
        await Assert.That(results).IsEquivalentTo(new List<long> { 4, 5, 6 });
    }

    [Test]
    public async Task DocumentForEachAtPath_ObjectWildcard_VisitsAllFieldValues()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2,"c":3}""");
        var results = new List<long>();
        doc.ForEachAtPath("$.*", v => results.Add(v.GetInt64()));
        await Assert.That(results.Count).IsEqualTo(3);
    }
}

// ─── ForEachAtPath on JsonArray / JsonObject ──────────────────────────────────

public class ForEachAtPathOnContainersTests
{
    [Test]
    public async Task ArrayForEachAtPath_Wildcard()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"items":[1,2,3]}""");
        using var arr = doc.GetField("items").GetArray();
        var results = new List<long>();
        arr.ForEachAtPath("$[*]", v => results.Add(v.GetInt64()));
        await Assert.That(results).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task ObjectForEachAtPath_AllFieldValues()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":10,"b":20,"c":30}""");
        using var obj = doc.GetObject();
        var results = new List<long>();
        obj.ForEachAtPath("$.*", v => results.Add(v.GetInt64()));
        await Assert.That(results.Count).IsEqualTo(3);
    }
}

// ─── Document scalar getters ──────────────────────────────────────────────────
