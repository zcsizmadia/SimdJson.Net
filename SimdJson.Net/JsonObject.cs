using System.Collections;
using System.Text;
using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// A JSON object obtained from a parsed document.
/// Implements <see cref="IEnumerable{T}"/> over <see cref="JsonProperty"/> for idiomatic foreach.
/// Disposing releases the native handle; the document must remain alive.
/// </summary>
public sealed class JsonObject : IDisposable, IEnumerable<JsonProperty>
{
    internal nint Handle;
    private readonly JsonDocument _owner;
    private bool _disposed;

    internal JsonObject(nint handle, JsonDocument owner)
    {
        Handle = handle;
        _owner = owner;
    }

    /// <summary>
    /// Returns the number of fields (requires a full native scan — use sparingly).
    /// </summary>
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ObjectCount(Handle, out nuint n));
            return (int)n;
        }
    }

    /// <summary>Gets a field by key (order-insensitive lookup).</summary>
    public unsafe JsonValue GetField(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(key, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ObjectGetFieldByKey(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <inheritdoc cref="GetField(string)"/>
    public JsonValue this[string key] => GetField(key);

    /// <summary>Iterates over all key-value pairs.</summary>
    public IEnumerator<JsonProperty> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ObjectBegin(Handle, out nint iter));
        try
        {
            while (true)
            {
                NextField(iter, out string? key, out nint valHandle, out bool done);
                if (done) yield break;
                yield return new JsonProperty(key!, new JsonValue(valHandle, _owner));
            }
        }
        finally
        {
            NativeMethods.DestroyObjectIter(iter);
        }
    }

    private static unsafe void NextField(nint iter, out string? key, out nint valHandle, out bool done)
    {
        SimdJsonException.ThrowIfError(
            NativeMethods.ObjectIterNext(iter,
                out byte* keyPtr, out nuint keyLen,
                out valHandle,
                out int doneInt));
        done = doneInt != 0;
        key = done ? null : System.Text.Encoding.UTF8.GetString(keyPtr, (int)keyLen);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyObject(Handle);
        Handle = 0;
    }
}

/// <summary>A key-value pair from a <see cref="JsonObject"/> iteration.</summary>
public readonly struct JsonProperty(string name, JsonValue value)
{
    /// <summary>The field name (unescaped).</summary>
    public string Name { get; } = name;

    /// <summary>The field value. Dispose when no longer needed.</summary>
    public JsonValue Value { get; } = value;
}
