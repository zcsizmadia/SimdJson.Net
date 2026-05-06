using System.Text;
using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// A reusable, thread-local-friendly simdjson On-Demand parser.
/// One parser instance should be used per thread; it holds a growing internal buffer
/// that is reused across <see cref="Parse"/> calls to avoid allocations.
/// </summary>
/// <remarks>
/// Disposing releases the native parser handle. After disposal the instance must not
/// be used. Use <see cref="SimdJsonParser.Shared"/> for a convenient thread-local instance.
/// </remarks>
public sealed class SimdJsonParser : IDisposable
{
    private nint _handle;
    private bool _disposed;

    [ThreadStatic]
    private static SimdJsonParser? _shared;

    /// <summary>
    /// A thread-local <see cref="SimdJsonParser"/> instance.
    /// Do not dispose this instance — it is owned by the thread.
    /// </summary>
    public static SimdJsonParser Shared => _shared ??= new SimdJsonParser();

    /// <summary>
    /// Returns the simdjson library version string (e.g. <c>"4.6.3"</c>).
    /// </summary>
    public static unsafe string GetVersion()
    {
        byte* p = NativeMethods.GetVersion();
        if (p == null) return string.Empty;
        int len = 0;
        while (p[len] != 0) len++;
        return System.Text.Encoding.UTF8.GetString(p, len);
    }

    /// <summary>Creates a new parser instance.</summary>
    public SimdJsonParser()
    {
        _handle = NativeMethods.CreateParser();
        if (_handle == 0)
            throw new InvalidOperationException("Failed to create native SimdJson parser.");
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
        _handle = NativeMethods.CreateParserWithCapacity(maxCapacity);
        if (_handle == 0)
            throw new InvalidOperationException("Failed to create native SimdJson parser.");
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
    /// The document's lifetime is independent of this parser but only one document
    /// may be in use per parser at a time (simdjson On-Demand constraint).
    /// </summary>
    public unsafe JsonDocument Parse(ReadOnlySpan<byte> utf8Json)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint docHandle;
        int err;
        fixed (byte* p = utf8Json)
            err = NativeMethods.Parse(_handle, p, (nuint)utf8Json.Length, out docHandle);

        SimdJsonException.ThrowIfError(err);
        return new JsonDocument(docHandle);
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
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Parses a UTF-16 .NET string asynchronously (transcoding happens on a thread-pool thread).
    /// </summary>
    public Task<JsonDocument> ParseAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        // P/Invoke is CPU-bound and fast; offload to thread pool so the caller's thread is freed.
        return Task.Run(() => Parse(json), cancellationToken);
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
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint docHandle;
        int err;
        fixed (byte* p = utf8Json)
            err = NativeMethods.ParseAllowIncompleteJson(_handle, p, (nuint)utf8Json.Length, out docHandle);

        SimdJsonException.ThrowIfError(err);
        return new JsonDocument(docHandle);
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
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyParser(_handle);
        _handle = 0;
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
        if (utf8Json.IsEmpty) return [];
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
        if (bytes.IsEmpty) return true;
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
