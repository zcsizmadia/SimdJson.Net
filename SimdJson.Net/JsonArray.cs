using System.Collections;
using System.Runtime.InteropServices;
using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// A JSON array obtained from a parsed document.
/// Implements <see cref="IEnumerable{T}"/> over <see cref="JsonValue"/> for idiomatic foreach.
/// Disposing releases the native handle; the document must remain alive.
/// </summary>
public sealed class JsonArray : IDisposable, IEnumerable<JsonValue>
{
    internal nint Handle;
    private readonly JsonDocument _owner;
    private bool _disposed;

    internal JsonArray(nint handle, JsonDocument owner)
    {
        Handle = handle;
        _owner = owner;
    }

    /// <summary>
    /// Throws if this array, or the document that owns its native memory, has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owner is { IsDisposed: true })
        {
            throw new ObjectDisposedException(nameof(JsonDocument),
                "The JsonDocument that owns this array has been disposed.");
        }
    }

    /// <summary>
    /// Returns the number of elements (requires a full native scan — use sparingly).
    /// </summary>
    public int Count
    {
        get
        {
            ThrowIfDisposed();
            SimdJsonException.ThrowIfError(NativeMethods.ArrayCount(Handle, out nuint n));
            return (int)n;
        }
    }

    /// <summary>Iterates over the array elements.</summary>
    public IEnumerator<JsonValue> GetEnumerator()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayBegin(Handle, out nint iter));
        try
        {
            while (true)
            {
                SimdJsonException.ThrowIfError(NativeMethods.ArrayIterNext(iter, out nint valHandle, out int done));
                if (done != 0)
                {
                    yield break;
                }

                yield return new JsonValue(valHandle, _owner);
            }
        }
        finally
        {
            NativeMethods.DestroyArrayIter(iter);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the element at the given zero-based <paramref name="index"/>.
    /// Iterates to that position (O(n)) — prefer <see cref="GetEnumerator"/> for sequential access.
    /// Throws <see cref="SimdJsonException"/> if the index is out of bounds.
    /// </summary>
    public JsonValue ElementAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayBegin(Handle, out nint iter));
        try
        {
            for (int i = 0; ; i++)
            {
                SimdJsonException.ThrowIfError(
                    NativeMethods.ArrayIterNext(iter, out nint valHandle, out int done));
                if (done != 0)
                {
                    throw new SimdJsonException(-4); // index out of bounds
                }

                if (i == index)
                {
                    return new JsonValue(valHandle, _owner);
                }
                // Discard intermediate elements
                NativeMethods.DestroyValue(valHandle);
            }
        }
        finally
        {
            NativeMethods.DestroyArrayIter(iter);
        }
    }

    /// <summary>
    /// Returns the element at a zero-based <paramref name="index"/> using the native
    /// <c>array.at()</c> method. Faster than <see cref="ElementAt"/> for random access.
    /// Throws <see cref="SimdJsonException"/> if the index is out of bounds.
    /// </summary>
    public JsonValue At(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayAt(Handle, (nuint)index, out nint h));
        return new JsonValue(h, _owner);
    }

    /// <summary>
    /// Returns the full raw JSON of this array as a string.
    /// This operation consumes the array iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe string GetRawJson()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayRawJson(Handle, out byte* ptr, out nuint len));
        return System.Text.Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Returns the full raw JSON of this array as a <see cref="ReadOnlySpan{T}"/> without allocating a string.
    /// This operation consumes the array iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetRawJsonSpan()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayRawJson(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>Resets the array iterator so the array can be traversed again.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayReset(Handle));
    }

    // ── Type predicates ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> if the array contains no elements.
    /// This is faster than checking <c>Count == 0</c> because it avoids a full scan.
    /// </summary>
    public bool IsEmpty()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ArrayIsEmpty(Handle, out int v));
        return v != 0;
    }

    // ── JSON Pointer and JSONPath ──────────────────────────────────────────────

    /// <summary>
    /// Gets a value via a JSON Pointer path starting from this array (e.g. <c>"/0/name"</c>).
    /// </summary>
    public unsafe JsonValue AtPointer(string pointer)
    {
        ThrowIfDisposed();
        int maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(pointer.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = System.Text.Encoding.UTF8.GetBytes(pointer, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ArrayAtPointer(Handle, p, out nint h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>
    /// Gets a value via a JSONPath expression starting from this array (e.g. <c>"$[0].name"</c>).
    /// </summary>
    public unsafe JsonValue AtPath(string jsonPath)
    {
        ThrowIfDisposed();
        int maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(jsonPath.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = System.Text.Encoding.UTF8.GetBytes(jsonPath, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ArrayAtPath(Handle, p, out nint h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>Tries to get a value via a JSON Pointer path.</summary>
    public bool TryAtPointer(string pointer, out JsonValue? value)
    {
        try { value = AtPointer(pointer); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { value = null; return false; }
    }

    /// <summary>Tries to get a value via a JSONPath expression.</summary>
    public bool TryAtPath(string jsonPath, out JsonValue? value)
    {
        try { value = AtPath(jsonPath); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { value = null; return false; }
    }

    /// <summary>
    /// Iterates over all values in this array that match the given JSONPath wildcard expression
    /// (e.g. <c>"$[*]"</c>, <c>"$[*].name"</c>) and invokes <paramref name="callback"/> for each match.
    /// </summary>
    /// <remarks>
    /// The <see cref="JsonValue"/> passed to <paramref name="callback"/> is borrowed —
    /// valid only during the callback, must not be disposed or stored.
    /// </remarks>
    public unsafe void ForEachAtPath(string path, Action<JsonValue> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(callback);
        int maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(path.Length);
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = System.Text.Encoding.UTF8.GetBytes(path, buf);
        var context = new JsonValue.WildcardContext(callback);
        var gcHandle = GCHandle.Alloc(context);
        try
        {
            int err;
            fixed (byte* p = buf)
            {
                err = NativeMethods.ArrayForEachAtPath(
                    Handle, p, (nuint)len, JsonValue.s_wildcardTrampolinePtr, GCHandle.ToIntPtr(gcHandle));
            }
            context.Rethrow();
            SimdJsonException.ThrowIfError(err);
        }
        finally { gcHandle.Free(); }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.DestroyArray(Handle);
        Handle = 0;
    }
}
