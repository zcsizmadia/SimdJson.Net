using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// A forward-only stream of JSON documents parsed from one in-memory buffer, backed by
/// simdjson's <c>iterate_many</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the batching parser: simdjson indexes whole batches of input at a time rather than
/// one document per call, and where the native library was built with threads it overlaps the
/// next batch's indexing with the current batch's parsing.
/// </para>
/// <para>
/// <b>The whole input must be in memory.</b> <see cref="NdjsonParserOptions.BatchSize"/> bounds
/// the index, not the input. For inputs too large to hold at once, keep using the
/// <see cref="System.IO.Stream"/> overloads of <see cref="NdjsonParser"/>, which read
/// incrementally.
/// </para>
/// <para>
/// The <see cref="JsonDocument"/> from <see cref="MoveNext"/> borrows the stream's current
/// document. It is invalidated by the following <see cref="MoveNext"/> and must not be used
/// after that or after the stream is disposed.
/// </para>
/// </remarks>
public sealed class JsonDocumentStream : IDisposable
{
    private nint _handle;
    private readonly SimdJsonParser _parser;
    private JsonDocument? _current;
    private bool _disposed;

    /// <summary>
    /// The stream owns the parser that produced it: simdjson's document_stream keeps using the
    /// parser's buffers for the whole iteration, so it cannot be shared or released earlier.
    /// </summary>
    internal JsonDocumentStream(nint handle, SimdJsonParser parser)
    {
        _handle = handle;
        _parser = parser;
    }

    /// <summary>
    /// The document produced by the last successful <see cref="MoveNext"/>, or
    /// <see langword="null"/> before the first call and after the stream is exhausted.
    /// </summary>
    public JsonDocument? Current => _current;

    /// <summary>
    /// Advances to the next document. Returns <see langword="false"/> at the end of the stream.
    /// </summary>
    /// <remarks>
    /// A document that fails to parse throws <see cref="SimdJsonException"/>. The stream is not
    /// ended by that: calling <see cref="MoveNext"/> again skips the bad document and continues,
    /// which is how <see cref="NdjsonParserOptions.SkipMalformedLines"/> is implemented.
    /// </remarks>
    public bool MoveNext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The previous document points into the stream and dies on advance.
        InvalidateCurrent();

        int err = NativeMethods.StreamNext(_handle, out nint docHandle, out int done);
        SimdJsonException.ThrowIfError(err);

        if (done != 0)
        {
            return false;
        }

        _current = new JsonDocument(docHandle, parser: null);
        return true;
    }

    /// <summary>Byte offset of the current document within the input.</summary>
    public nuint CurrentIndex
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.StreamCurrentIndex(_handle, out nuint v));
            return v;
        }
    }

    /// <summary>
    /// The raw JSON text of the current document, as UTF-8 bytes pointing into the stream's
    /// buffer. Valid until the next <see cref="MoveNext"/>.
    /// </summary>
    public unsafe ReadOnlySpan<byte> CurrentSource
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(
                NativeMethods.StreamSource(_handle, out byte* ptr, out nuint len));
            return new ReadOnlySpan<byte>(ptr, (int)len);
        }
    }

    /// <summary>Total size of the input in bytes.</summary>
    public nuint SizeInBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.StreamSizeInBytes(_handle, out nuint v));
            return v;
        }
    }

    /// <summary>
    /// Bytes left unparsed at the end of the input, typically an incomplete trailing document.
    /// </summary>
    /// <remarks>
    /// Only meaningful once the stream has been read to the end with no document reporting an
    /// error. simdjson documents the value as arbitrary outside those conditions: it can exceed
    /// <see cref="SizeInBytes"/> or wrap around. To detect a truncated tail in other situations,
    /// track <see cref="CurrentIndex"/> of the last document you read successfully.
    /// </remarks>
    public nuint TruncatedBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.StreamTruncatedBytes(_handle, out nuint v));
            return v;
        }
    }

    private void InvalidateCurrent()
    {
        _current?.Dispose();
        _current = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Order matters: documents borrow the stream, and the stream uses the parser.
        InvalidateCurrent();
        NativeMethods.DestroyStream(_handle);
        _handle = 0;
        _parser.Dispose();
    }
}
