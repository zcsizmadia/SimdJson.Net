using System.Text;
using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// A reusable, thread-local-friendly simdjson On-Demand parser.
/// One parser instance should be used per thread; it holds a growing internal buffer
/// that is reused across <see cref="Parse"/> calls to avoid allocations.
/// </summary>
/// <remarks>
/// <para>
/// Disposing releases the native parser handle. After disposal the instance must not
/// be used. Use <see cref="SimdJsonParser.Shared"/> for a convenient thread-local instance.
/// </para>
/// <para>
/// Only one <see cref="JsonDocument"/> may be alive per parser at a time. Calling
/// <see cref="Parse(ReadOnlySpan{byte})"/> while a previous document from the same parser has
/// not been disposed throws <see cref="InvalidOperationException"/>. Disposing the parser also
/// disposes its live document.
/// </para>
/// </remarks>
public sealed class SimdJsonParser : IDisposable
{
    private nint _handle;
    private bool _disposed;
    private JsonDocument? _liveDocument;

    [ThreadStatic]
    private static SimdJsonParser? _shared;

    /// <summary>
    /// A thread-local <see cref="SimdJsonParser"/> instance.
    /// Do not dispose this instance — it is owned by the thread.
    /// </summary>
    /// <remarks>
    /// The instance is bound to the calling thread. Do not hold a document obtained from
    /// <see cref="Shared"/> across an <c>await</c>: the continuation may run on another thread
    /// whose own <see cref="Shared"/> parser is unrelated, and other work resumed on the original
    /// thread would find the parser still occupied.
    /// </remarks>
    public static SimdJsonParser Shared => _shared ??= new SimdJsonParser();

    /// <summary>
    /// Returns the simdjson library version string (e.g. <c>"4.6.11"</c>).
    /// </summary>
    public static unsafe string GetVersion()
    {
        byte* p = NativeMethods.GetVersion();
        if (p == null)
        {
            return string.Empty;
        }

        int len = 0;
        while (p[len] != 0)
        {
            len++;
        }

        return System.Text.Encoding.UTF8.GetString(p, len);
    }

    /// <summary>Creates a new parser instance.</summary>
    public SimdJsonParser()
    {
        _handle = NativeMethods.CreateParser();
        if (_handle == 0)
        {
            throw new InvalidOperationException("Failed to create native SimdJson parser.");
        }
    }

    /// <summary>
    /// Creates a new parser instance with a custom maximum document capacity.
    /// </summary>
    /// <param name="maxCapacity">
    /// Maximum document size in bytes. Attempts to parse larger documents will fail.
    /// Pass 0 to use the simdjson default (typically 4 GiB).
    /// </param>
    public SimdJsonParser(nuint maxCapacity)
    {
        _handle = maxCapacity == 0
            ? NativeMethods.CreateParser()
            : NativeMethods.CreateParserWithCapacity(maxCapacity);
        if (_handle == 0)
        {
            throw new InvalidOperationException("Failed to create native SimdJson parser.");
        }
    }

    /// <summary>
    /// Returns the current internal buffer capacity in bytes.
    /// Returns 0 if no document has been parsed yet.
    /// </summary>
    public nuint Capacity
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ParserCapacity(_handle, out nuint v));
            return v;
        }
    }

    /// <summary>Returns the maximum allowed document size in bytes.</summary>
    public nuint MaxCapacity
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ParserMaxCapacity(_handle, out nuint v));
            return v;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ParserSetMaxCapacity(_handle, value));
        }
    }

    /// <summary>Returns the maximum JSON nesting depth this parser supports.</summary>
    public nuint MaxDepth
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ParserMaxDepth(_handle, out nuint v));
            return v;
        }
    }

    /// <summary>
    /// Parses a UTF-8 JSON span and returns an owning <see cref="JsonDocument"/>.
    /// The returned document borrows this parser's internal buffers: it must be disposed
    /// before the next <c>Parse</c> call and before the parser is disposed. Strings and spans
    /// read from the document are valid only while the document is alive.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A previous <see cref="JsonDocument"/> from this parser has not been disposed.
    /// </exception>
    public unsafe JsonDocument Parse(ReadOnlySpan<byte> utf8Json)
    {
        ThrowIfBusy();

        nint docHandle;
        int err;
        if (utf8Json.IsEmpty)
        {
            byte empty = 0;
            err = NativeMethods.Parse(_handle, &empty, 0, out docHandle);
        }
        else
        {
            fixed (byte* p = utf8Json)
            {
                err = NativeMethods.Parse(_handle, p, (nuint)utf8Json.Length, out docHandle);
            }
        }

        SimdJsonException.ThrowIfError(err);
        return AttachDocument(docHandle);
    }

    private void ThrowIfBusy()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_liveDocument is { IsDisposed: false })
        {
            throw new InvalidOperationException(
                "The previous JsonDocument produced by this SimdJsonParser has not been disposed. " +
                "A parser supports one live document at a time; dispose it before parsing again, " +
                "or use a separate SimdJsonParser instance.");
        }
    }

    private JsonDocument AttachDocument(nint docHandle)
    {
        var doc = new JsonDocument(docHandle, this);
        _liveDocument = doc;
        return doc;
    }

    internal void OnDocumentDisposed(JsonDocument doc)
    {
        if (ReferenceEquals(_liveDocument, doc))
        {
            _liveDocument = null;
        }
    }

    /// <summary>
    /// Parses a UTF-16 .NET string by transcoding to UTF-8 on the stack/heap and
    /// returning an owning <see cref="JsonDocument"/>.
    /// </summary>
    public JsonDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        int maxBytes = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rented = null;

        Span<byte> buffer = maxBytes <= 4096
            ? stackalloc byte[maxBytes]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            int written = Encoding.UTF8.GetBytes(json, buffer);
            return Parse(buffer[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Parses a UTF-16 .NET string and returns the result as a completed <see cref="Task{TResult}"/>.
    /// </summary>
    /// <remarks>
    /// Parsing is CPU-bound and typically takes microseconds, so the work runs synchronously on the
    /// calling thread. Running it on a thread-pool thread would make <see cref="Shared"/> unsafe,
    /// because that parser is bound to the thread that obtained it.
    /// </remarks>
    public Task<JsonDocument> ParseAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<JsonDocument>(cancellationToken);
        }

        try
        {
            return Task.FromResult(Parse(json));
        }
        catch (Exception ex)
        {
            return Task.FromException<JsonDocument>(ex);
        }
    }

    /// <summary>
    /// Reads all bytes from <paramref name="stream"/> and parses them as UTF-8 JSON.
    /// </summary>
    public async Task<JsonDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var ms = new System.IO.MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return Parse(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    /// <summary>
    /// Parses a UTF-8 JSON span that may be truncated (e.g. a partial download or streamed buffer).
    /// Equivalent to <c>parser::iterate_allow_incomplete_json()</c>.
    /// </summary>
    public unsafe JsonDocument ParseAllowIncompleteJson(ReadOnlySpan<byte> utf8Json)
    {
        ThrowIfBusy();

        nint docHandle;
        int err;
        if (utf8Json.IsEmpty)
        {
            byte empty = 0;
            err = NativeMethods.ParseAllowIncompleteJson(_handle, &empty, 0, out docHandle);
        }
        else
        {
            fixed (byte* p = utf8Json)
            {
                err = NativeMethods.ParseAllowIncompleteJson(_handle, p, (nuint)utf8Json.Length, out docHandle);
            }
        }

        SimdJsonException.ThrowIfError(err);
        return AttachDocument(docHandle);
    }

    /// <summary>
    /// Parses a UTF-16 .NET string as potentially truncated JSON.
    /// </summary>
    public JsonDocument ParseAllowIncompleteJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        int maxBytes = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rented = null;

        Span<byte> buffer = maxBytes <= 4096
            ? stackalloc byte[maxBytes]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            int written = Encoding.UTF8.GetBytes(json, buffer);
            return ParseAllowIncompleteJson(buffer[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _liveDocument?.Dispose();
        _liveDocument = null;
        NativeMethods.DestroyParser(_handle);
        _handle = 0;

        if (ReferenceEquals(_shared, this))
        {
            _shared = null;
        }
    }

    // ── Static utilities (no parser instance required) ────────────────────────

    /// <summary>
    /// Minifies a JSON string by removing all insignificant whitespace.
    /// </summary>
    /// <param name="json">The UTF-16 JSON string to minify.</param>
    /// <returns>The minified JSON string.</returns>
    public static unsafe string Minify(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[] inputBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
        byte[] outputBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int inputLen = Encoding.UTF8.GetBytes(json, inputBuf);
            nuint outLen;
            fixed (byte* pIn = inputBuf, pOut = outputBuf)
            {
                SimdJsonException.ThrowIfError(
                    NativeMethods.Minify(pIn, (nuint)inputLen, pOut, out outLen));
            }
            return Encoding.UTF8.GetString(outputBuf, 0, (int)outLen);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(inputBuf);
            System.Buffers.ArrayPool<byte>.Shared.Return(outputBuf);
        }
    }

    /// <summary>
    /// Minifies a UTF-8 JSON byte span by removing all insignificant whitespace.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON to minify.</param>
    /// <returns>A new byte array containing the minified UTF-8 JSON.</returns>
    public static unsafe byte[] MinifyUtf8(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            return [];
        }

        byte[] outputBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(utf8Json.Length);
        try
        {
            nuint outLen;
            fixed (byte* pIn = utf8Json, pOut = outputBuf)
            {
                SimdJsonException.ThrowIfError(
                    NativeMethods.Minify(pIn, (nuint)utf8Json.Length, pOut, out outLen));
            }
            return outputBuf[..(int)outLen];
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(outputBuf);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given bytes constitute valid UTF-8.
    /// This performs pure UTF-8 validation without parsing JSON.
    /// </summary>
    public static unsafe bool ValidateUtf8(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return true;
        }

        fixed (byte* p = bytes)
        {
            return NativeMethods.ValidateUtf8(p, (nuint)bytes.Length) != 0;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given string, when encoded to UTF-8, produces valid UTF-8.
    /// Since .NET strings are UTF-16, this encodes to UTF-8 first and then validates.
    /// </summary>
    public static bool ValidateUtf8(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        // All valid .NET strings produce valid UTF-8 when transcoded.
        // This is primarily useful for byte buffers, but provided for completeness.
        return ValidateUtf8(Encoding.UTF8.GetBytes(text));
    }
}
