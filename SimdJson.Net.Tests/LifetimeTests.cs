namespace SimdJson.Tests;

// ─── Parser / document lifetime rules ─────────────────────────────────────────

public class ParserLifetimeTests
{
    [Test]
    public async Task Parse_WhileDocumentAlive_Throws()
    {
        using var parser = new SimdJsonParser();
        using var doc = parser.Parse("""{"a":1}""");
        await Assert.That(() => parser.Parse("""{"b":2}""")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Parse_AfterDocumentDisposed_Succeeds()
    {
        using var parser = new SimdJsonParser();
        using (var first = parser.Parse("""{"a":1}"""))
        {
            using var a = first.GetField("a");
            await Assert.That(a.GetInt64()).IsEqualTo(1L);
        }

        using var second = parser.Parse("""{"b":2}""");
        using var b = second.GetField("b");
        await Assert.That(b.GetInt64()).IsEqualTo(2L);
    }

    [Test]
    public async Task ParserDispose_DisposesLiveDocument()
    {
        var parser = new SimdJsonParser();
        var doc = parser.Parse("""{"a":1}""");
        parser.Dispose();
        await Assert.That(() => doc.ValueKind).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ZeroMaxCapacity_UsesLibraryDefault()
    {
        using var parser = new SimdJsonParser(0);
        using var doc = parser.Parse("""{"a":1}""");
        using var a = doc.GetField("a");
        await Assert.That(a.GetInt64()).IsEqualTo(1L);
    }

    [Test]
    public async Task ExplicitMaxCapacity_TooSmall_ThrowsCapacity()
    {
        using var parser = new SimdJsonParser(4);
        var ex = await Assert.That(() => parser.Parse("""{"key":"a longer document"}"""))
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-1);
    }

    [Test]
    public async Task ParseAsync_String_ReturnsUsableDocument()
    {
        using var parser = new SimdJsonParser();
        using var doc = await parser.ParseAsync("""{"a":7}""");
        using var a = doc.GetField("a");
        await Assert.That(a.GetInt64()).IsEqualTo(7L);
    }

    [Test]
    public async Task Parse_EmptyInput_ThrowsParseError_NotNullPointer()
    {
        using var parser = new SimdJsonParser();
        var ex = await Assert.That(() => parser.Parse(ReadOnlySpan<byte>.Empty.ToArray().AsSpan()))
            .Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-6);
    }

    [Test]
    public async Task Parse_EmptyString_ThrowsParseError_NotNullPointer()
    {
        using var parser = new SimdJsonParser();
        var ex = await Assert.That(() => parser.Parse("")).Throws<SimdJsonException>();
        await Assert.That(ex!.ErrorCode).IsEqualTo(-6);
    }
}

// ─── Children of a disposed document ──────────────────────────────────────────

public class DisposedDocumentTests
{
    [Test]
    public async Task Value_AfterDocumentDisposed_ThrowsObjectDisposed()
    {
        using var parser = new SimdJsonParser();
        var doc = parser.Parse("""{"a":"hello"}""");
        var val = doc.GetField("a");
        doc.Dispose();
        await Assert.That(() => val.GetString()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Array_AfterDocumentDisposed_ThrowsObjectDisposed()
    {
        using var parser = new SimdJsonParser();
        var doc = parser.Parse("""[1,2,3]""");
        var arr = doc.GetArray();
        doc.Dispose();
        await Assert.That(() => arr.Count).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Object_AfterDocumentDisposed_ThrowsObjectDisposed()
    {
        using var parser = new SimdJsonParser();
        var doc = parser.Parse("""{"a":1}""");
        var obj = doc.GetObject();
        doc.Dispose();
        await Assert.That(() => obj.Count).Throws<ObjectDisposedException>();
    }
}

// ─── Wildcard callback exception propagation ──────────────────────────────────

public class WildcardCallbackTests
{
    private sealed class MarkerException : Exception;

    [Test]
    public async Task DocumentForEachAtPath_CallbackThrows_ExceptionSurfacesToCaller()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        await Assert.That(() => doc.ForEachAtPath("$[*]", _ => throw new MarkerException()))
            .Throws<MarkerException>();
    }

    [Test]
    public async Task DocumentForEachAtPath_CallbackThrows_StopsAfterFirstFailure()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        int invocations = 0;
        try
        {
            doc.ForEachAtPath("$[*]", _ =>
            {
                invocations++;
                throw new MarkerException();
            });
        }
        catch (MarkerException)
        {
        }

        await Assert.That(invocations).IsEqualTo(1);
    }

    [Test]
    public async Task ValueForEachAtPath_CallbackThrows_ExceptionSurfacesToCaller()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"items":[1,2]}""");
        using var items = doc.GetField("items");
        await Assert.That(() => items.ForEachAtPath("$[*]", _ => throw new MarkerException()))
            .Throws<MarkerException>();
    }

    [Test]
    public async Task ArrayForEachAtPath_CallbackThrows_ExceptionSurfacesToCaller()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2]""");
        using var arr = doc.GetArray();
        await Assert.That(() => arr.ForEachAtPath("$[*]", _ => throw new MarkerException()))
            .Throws<MarkerException>();
    }

    [Test]
    public async Task ObjectForEachAtPath_CallbackThrows_ExceptionSurfacesToCaller()
    {
        using var doc = SimdJsonParser.Shared.Parse("""{"a":1,"b":2}""");
        using var obj = doc.GetObject();
        await Assert.That(() => obj.ForEachAtPath("$.*", _ => throw new MarkerException()))
            .Throws<MarkerException>();
    }

    [Test]
    public async Task ForEachAtPath_BorrowedValue_StoredAndUsedLater_Throws()
    {
        using var doc = SimdJsonParser.Shared.Parse("""[1,2,3]""");
        JsonValue? escaped = null;
        doc.ForEachAtPath("$[*]", v => escaped ??= v);
        await Assert.That(() => escaped!.GetInt64()).Throws<ObjectDisposedException>();
    }
}
