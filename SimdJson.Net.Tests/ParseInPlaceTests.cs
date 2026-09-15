using System.Buffers;
using System.Text;

namespace SimdJson.Tests;

// ─── Zero-copy parsing from a caller-owned buffer ─────────────────────────────

public class ParseInPlaceTests
{
    /// <summary>Allocates a buffer holding <paramref name="json"/> plus the required padding.</summary>
    private static (byte[] Buffer, int Length) Padded(string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        byte[] buffer = new byte[utf8.Length + SimdJsonParser.RequiredPadding];
        utf8.CopyTo(buffer, 0);
        return (buffer, utf8.Length);
    }

    [Test]
    public async Task RequiredPadding_IsPositive()
    {
        await Assert.That(SimdJsonParser.RequiredPadding).IsGreaterThan(0);
    }

    [Test]
    public async Task ParseInPlace_ReadsObjectFields()
    {
        var (buffer, length) = Padded("""{"name":"Alice","age":30}""");
        using var parser = new SimdJsonParser();
        using var doc = parser.ParseInPlace(buffer, length);

        using var name = doc.GetField("name");
        await Assert.That(name.GetString()).IsEqualTo("Alice");
    }

    [Test]
    public async Task ParseInPlace_ReadsArrayElements()
    {
        var (buffer, length) = Padded("""[10,20,30]""");
        using var parser = new SimdJsonParser();
        using var doc = parser.ParseInPlace(buffer, length);
        using var arr = doc.GetArray();
        await Assert.That(arr.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ParseInPlace_MatchesCopyingParse()
    {
        const string json = """{"a":[1,2,{"b":"x"}],"c":true}""";
        var (buffer, length) = Padded(json);

        using var inPlaceParser = new SimdJsonParser();
        using var inPlace = inPlaceParser.ParseInPlace(buffer, length);
        string rawInPlace = inPlace.GetRawJson();

        using var copyParser = new SimdJsonParser();
        using var copied = copyParser.Parse(json);
        await Assert.That(rawInPlace).IsEqualTo(copied.GetRawJson());
    }

    [Test]
    public async Task ParseInPlace_IgnoresBytesPastTheJson()
    {
        // The padding region is read but its contents must not affect the result.
        var (buffer, length) = Padded("""{"a":1}""");
        buffer.AsSpan(length).Fill((byte)'Z');

        using var parser = new SimdJsonParser();
        using var doc = parser.ParseInPlace(buffer, length);
        using var a = doc.GetField("a");
        await Assert.That(a.GetInt64()).IsEqualTo(1L);
    }

    [Test]
    public async Task ParseInPlace_WorksWithAPooledBuffer()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"pooled":true}""");
        byte[] rented = ArrayPool<byte>.Shared.Rent(utf8.Length + SimdJsonParser.RequiredPadding);
        try
        {
            utf8.CopyTo(rented, 0);
            using var parser = new SimdJsonParser();
            using var doc = parser.ParseInPlace(rented, utf8.Length);
            using var v = doc.GetField("pooled");
            await Assert.That(v.GetBool()).IsTrue();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public async Task ParseInPlace_InsufficientPadding_ThrowsArgumentOutOfRange()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"a":1}""");
        byte[] tooSmall = new byte[utf8.Length + SimdJsonParser.RequiredPadding - 1];
        utf8.CopyTo(tooSmall, 0);

        using var parser = new SimdJsonParser();
        await Assert.That(() => parser.ParseInPlace(tooSmall, utf8.Length))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ParseInPlace_NoPaddingAtAll_ThrowsArgumentOutOfRange()
    {
        byte[] exact = Encoding.UTF8.GetBytes("""{"a":1}""");
        using var parser = new SimdJsonParser();
        await Assert.That(() => parser.ParseInPlace(exact, exact.Length))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ParseInPlace_NegativeLength_ThrowsArgumentOutOfRange()
    {
        var (buffer, _) = Padded("""{"a":1}""");
        using var parser = new SimdJsonParser();
        await Assert.That(() => parser.ParseInPlace(buffer, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ParseInPlace_LengthBeyondBuffer_ThrowsArgumentOutOfRange()
    {
        var (buffer, _) = Padded("""{"a":1}""");
        using var parser = new SimdJsonParser();
        await Assert.That(() => parser.ParseInPlace(buffer, buffer.Length + 1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ParseInPlace_MalformedJson_ThrowsOnAccess()
    {
        var (buffer, length) = Padded("""{"a":}""");
        using var parser = new SimdJsonParser();
        await Assert.That(() =>
        {
            using var doc = parser.ParseInPlace(buffer, length);
            using var v = doc.GetField("a");
            _ = v.GetInt64();
        }).Throws<SimdJsonException>();
    }

    // ── Lifetime rules carry over ─────────────────────────────────────────────

    [Test]
    public async Task ParseInPlace_WhileDocumentAlive_Throws()
    {
        var (buffer, length) = Padded("""{"a":1}""");
        using var parser = new SimdJsonParser();
        using var doc = parser.ParseInPlace(buffer, length);
        await Assert.That(() => parser.ParseInPlace(buffer, length))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ParseInPlace_AfterDocumentDisposed_CanParseAgain()
    {
        var (buffer, length) = Padded("""{"a":1}""");
        using var parser = new SimdJsonParser();
        using (var first = parser.ParseInPlace(buffer, length))
        {
            using var a = first.GetField("a");
            await Assert.That(a.GetInt64()).IsEqualTo(1L);
        }

        using var second = parser.ParseInPlace(buffer, length);
        using var b = second.GetField("a");
        await Assert.That(b.GetInt64()).IsEqualTo(1L);
    }

    [Test]
    public async Task ParseInPlace_ValueAfterDocumentDisposed_Throws()
    {
        var (buffer, length) = Padded("""{"a":"text"}""");
        using var parser = new SimdJsonParser();
        var doc = parser.ParseInPlace(buffer, length);
        var val = doc.GetField("a");
        doc.Dispose();
        await Assert.That(() => val.GetString()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ParseInPlace_RepeatedParses_DoNotLeakPins()
    {
        // Each parse pins the buffer and each dispose must release it; a leaked pin would
        // accumulate across iterations.
        var (buffer, length) = Padded("""{"a":1}""");
        using var parser = new SimdJsonParser();
        for (int i = 0; i < 200; i++)
        {
            using var doc = parser.ParseInPlace(buffer, length);
            using var a = doc.GetField("a");
            await Assert.That(a.GetInt64()).IsEqualTo(1L);
        }
    }
}
