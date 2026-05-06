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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyArray(Handle);
        Handle = 0;
    }
}
