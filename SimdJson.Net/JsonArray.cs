using System.Collections;
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
    /// Returns the number of elements (requires a full native scan — use sparingly).
    /// </summary>
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ArrayCount(Handle, out nuint n));
            return (int)n;
        }
    }

    /// <summary>Iterates over the array elements.</summary>
    public IEnumerator<JsonValue> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ArrayBegin(Handle, out nint iter));
        try
        {
            while (true)
            {
                SimdJsonException.ThrowIfError(NativeMethods.ArrayIterNext(iter, out nint valHandle, out int done));
                if (done != 0) yield break;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ArrayBegin(Handle, out nint iter));
        try
        {
            for (int i = 0; ; i++)
            {
                SimdJsonException.ThrowIfError(
                    NativeMethods.ArrayIterNext(iter, out nint valHandle, out int done));
                if (done != 0)
                    throw new SimdJsonException(-4); // index out of bounds
                if (i == index)
                    return new JsonValue(valHandle, _owner);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ArrayAt(Handle, (nuint)index, out nint h));
        return new JsonValue(h, _owner);
    }

    /// <summary>
    /// Returns the full raw JSON of this array as a string.
    /// This operation consumes the array iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe string GetRawJson()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ArrayRawJson(Handle, out byte* ptr, out nuint len));
        return System.Text.Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>Resets the array iterator so the array can be traversed again.</summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ArrayReset(Handle));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyArray(Handle);
        Handle = 0;
    }
}
