using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using SimdJson.Net.Internal;
using SimdJson.Net.Stage2;

namespace SimdJson.Net;

/// <summary>
/// Parsed JSON document. Holds the tape (flat representation of the value
/// tree) and the decoded string buffer. Construct via the static
/// <c>Parse</c> factories; values are accessed through <see cref="Root"/>.
/// </summary>
public sealed class JsonDocument : IDisposable
{
    private readonly long[] _tape;
    private readonly int _tapeLength;
    private readonly byte[] _stringBuffer;
    private readonly int _stringBufferLength;
    private readonly byte[]? _rentedInput;
    private bool _disposed;

    internal JsonDocument(long[] tape, int tapeLen, byte[] strBuf, int strLen, byte[]? rentedInput)
    {
        _tape = tape;
        _tapeLength = tapeLen;
        _stringBuffer = strBuf;
        _stringBufferLength = strLen;
        _rentedInput = rentedInput;
    }

    internal ReadOnlySpan<long> Tape => _tape.AsSpan(0, _tapeLength);
    internal ReadOnlySpan<byte> StringBuffer => _stringBuffer.AsSpan(0, _stringBufferLength);

    /// <summary>The root JSON element.</summary>
    public JsonElement Root => new(this, 0);

    // ---- Factories -------------------------------------------------------

    /// <summary>Parses a UTF-8 JSON document from a span. The span is copied internally.</summary>
    public static JsonDocument Parse(ReadOnlySpan<byte> utf8)
    {
        // Ensure trailing padding so the SIMD loop never reads past end-of-buffer.
        byte[] rented = ArrayPool<byte>.Shared.Rent(utf8.Length + StructuralIndexer.BlockSize);
        utf8.CopyTo(rented);
        rented.AsSpan(utf8.Length, StructuralIndexer.BlockSize).Fill((byte)' ');

        try
        {
            int max = StructuralIndexer.EstimateMaxIndices(utf8.Length);
            int[] structurals = ArrayPool<int>.Shared.Rent(max);
            try
            {
                int n = StructuralIndexer.Index(rented.AsSpan(0, utf8.Length), structurals);
                var builder = new TapeBuilder(rented, utf8.Length, structurals, n);
                builder.Build();
                return new JsonDocument(builder.Tape, builder.TapeLength,
                    builder.StringBuffer, builder.StringBufferLength, rented);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(structurals);
            }
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }

    /// <summary>Parses a UTF-16 string by transcoding to UTF-8 first.</summary>
    public static JsonDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int max = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[] buf = ArrayPool<byte>.Shared.Rent(max);
        try
        {
            int written = Encoding.UTF8.GetBytes(json, buf);
            return Parse(buf.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    /// <summary>Parses from a <see cref="ReadOnlyMemory{Byte}"/>.</summary>
    public static JsonDocument Parse(ReadOnlyMemory<byte> utf8) => Parse(utf8.Span);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_rentedInput is not null)
            ArrayPool<byte>.Shared.Return(_rentedInput);
        ArrayPool<long>.Shared.Return(_tape);
        ArrayPool<byte>.Shared.Return(_stringBuffer);
        GC.SuppressFinalize(this);
    }
}
