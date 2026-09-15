using System.Text;

namespace SimdJson.Tests;

// ─── Escaped key access during object iteration ───────────────────────────────

public class EscapedKeyTests
{
    [Test]
    public async Task EscapedName_PlainKey_MatchesName()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"plain":1}""");
        using var obj = doc.GetObject();
        foreach (var prop in obj)
        {
            await Assert.That(prop.Name).IsEqualTo("plain");
            await Assert.That(prop.EscapedName).IsEqualTo("plain");
            prop.Value.Dispose();
        }
    }

    [Test]
    public async Task EscapedName_KeyWithQuote_KeepsTheEscape()
    {
        // JSON source key is  a\"b  which unescapes to  a"b
        using var doc = SimdJsonParser.Shared.Parse("""{"a\"b":1}""");
        using var obj = doc.GetObject();
        foreach (var prop in obj)
        {
            await Assert.That(prop.Name).IsEqualTo("a\"b");
            await Assert.That(prop.EscapedName).IsEqualTo("a\\\"b");
            prop.Value.Dispose();
        }
    }

    [Test]
    public async Task EscapedName_KeyWithNewlineEscape_KeepsTheEscape()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a\nb":1}""");
        using var obj = doc.GetObject();
        foreach (var prop in obj)
        {
            await Assert.That(prop.Name).IsEqualTo("a\nb");
            await Assert.That(prop.EscapedName).IsEqualTo("a\\nb");
            prop.Value.Dispose();
        }
    }

    [Test]
    public async Task EscapedName_UnicodeEscape_KeepsTheEscape()
    {
        // Built with explicit escaping so the JSON text really contains a \u sequence
        // rather than the character it denotes.
        const string json = "{\"caf\\u00e9\":1}";
        using var doc = SimdJsonParser.Shared.Parse(json);
        using var obj = doc.GetObject();
        foreach (var prop in obj)
        {
            await Assert.That(prop.Name).IsEqualTo("café");
            await Assert.That(prop.EscapedName).IsEqualTo("caf\\u00e9");
            prop.Value.Dispose();
        }
    }

    [Test]
    public async Task EscapedName_UnicodeEscape_RoundTripsThroughGetField()
    {
        const string json = "{\"caf\\u00e9\":5}";
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse(json);

        string escaped;
        using (var obj = doc.GetObject())
        {
            var enumerator = obj.GetEnumerator();
            enumerator.MoveNext();
            escaped = enumerator.Current.EscapedName;
            enumerator.Current.Value.Dispose();
        }

        doc.Rewind();
        using var found = doc.GetField(escaped);
        await Assert.That(found.GetInt64()).IsEqualTo(5L);
    }

    [Test]
    public async Task EscapedNameSpan_MatchesEscapedName()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a\"b":1}""");
        using var obj = doc.GetObject();
        foreach (var prop in obj)
        {
            var fromSpan = Encoding.UTF8.GetString(prop.EscapedNameSpan);
            await Assert.That(fromSpan).IsEqualTo(prop.EscapedName);
            prop.Value.Dispose();
        }
    }

    // ── The round trip this feature exists for ────────────────────────────────

    [Test]
    public async Task EscapedName_RoundTripsThroughGetField()
    {
        // Collect the escaped key during iteration, then look the field up again with it.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a\"b":42}""");

        string escaped;
        using (var obj = doc.GetObject())
        {
            var enumerator = obj.GetEnumerator();
            enumerator.MoveNext();
            escaped = enumerator.Current.EscapedName;
            enumerator.Current.Value.Dispose();
        }

        doc.Rewind();
        using var found = doc.GetField(escaped);
        await Assert.That(found.GetInt64()).IsEqualTo(42L);
    }

    [Test]
    public async Task UnescapedName_DoesNotMatchGetField_ForAnEscapedKey()
    {
        // Documents the trap: simdjson compares against the raw escaped bytes, so the
        // unescaped Name cannot be fed back into a lookup when the key contains escapes.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a\"b":42}""");
        await Assert.That(() => doc.GetField("a\"b")).Throws<SimdJsonException>();
    }

    [Test]
    public async Task EscapedName_RoundTripsThroughObjectGetField()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"x\\y":7,"other":1}""");

        string escaped;
        using (var obj = doc.GetObject())
        {
            var enumerator = obj.GetEnumerator();
            enumerator.MoveNext();
            escaped = enumerator.Current.EscapedName;
            enumerator.Current.Value.Dispose();
        }

        doc.Rewind();
        using var obj2 = doc.GetObject();
        using var found = obj2.GetField(escaped);
        await Assert.That(found.GetInt64()).IsEqualTo(7L);
    }

    [Test]
    public async Task EscapedNameSpan_StaysValidAfterAdvancingTheEnumerator()
    {
        // Unlike Value, the escaped key points into the document buffer rather than the
        // iterator's scratch space, so it survives moving to the next field.
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"first":1,"second":2}""");
        using var obj = doc.GetObject();

        var enumerator = obj.GetEnumerator();
        enumerator.MoveNext();
        var firstProp = enumerator.Current;
        firstProp.Value.Dispose();

        enumerator.MoveNext();
        enumerator.Current.Value.Dispose();

        await Assert.That(Encoding.UTF8.GetString(firstProp.EscapedNameSpan)).IsEqualTo("first");
    }

    [Test]
    public async Task AllKeys_RoundTripInAMixedObject()
    {
        using var parser = new SimdJsonParser();
        const string json = """{"plain":1,"a\"b":2,"c\\d":3,"e\nf":4}""";
        using var doc = parser.Parse(json);

        var escapedKeys = new List<string>();
        using (var obj = doc.GetObject())
        {
            foreach (var prop in obj)
            {
                escapedKeys.Add(prop.EscapedName);
                prop.Value.Dispose();
            }
        }

        await Assert.That(escapedKeys.Count).IsEqualTo(4);

        for (int i = 0; i < escapedKeys.Count; i++)
        {
            doc.Rewind();
            using var found = doc.GetField(escapedKeys[i]);
            await Assert.That(found.GetInt64()).IsEqualTo(i + 1L);
        }
    }
}
