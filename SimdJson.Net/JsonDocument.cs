using System.Text;
using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// Owns a parsed simdjson On-Demand document.
/// Disposing releases the native memory. All child handles (<see cref="JsonValue"/>,
/// <see cref="JsonArray"/>, <see cref="JsonObject"/>) become invalid after disposal.
/// </summary>
public sealed class JsonDocument : IDisposable
{
    internal nint Handle;
    private bool _disposed;

    internal JsonDocument(nint handle) => Handle = handle;

    /// <summary>Gets the JSON type of the document root.</summary>
    public JsonValueKind ValueKind
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.DocumentGetType(Handle, out var kind));
            return kind;
        }
    }

    /// <summary>Gets the root as a <see cref="JsonArray"/>. Throws if root is not an array.</summary>
    public JsonArray GetArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.DocumentGetArray(Handle, out var h));
        return new JsonArray(h, this);
    }

    /// <summary>Gets the root as a <see cref="JsonObject"/>. Throws if root is not an object.</summary>
    public JsonObject GetObject()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.DocumentGetObject(Handle, out var h));
        return new JsonObject(h, this);
    }

    /// <summary>Gets a field from the root object by key.</summary>
    public unsafe JsonValue GetField(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(key, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.DocumentGetFieldByKey(Handle, p, out var h));
            return new JsonValue(h, this);
        }
    }

    /// <summary>Gets a value at a JSON Pointer path (e.g. <c>"/items/0/name"</c>).</summary>
    public unsafe JsonValue AtPointer(string pointer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(pointer.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(pointer, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.DocumentAtPointer(Handle, p, out var h));
            return new JsonValue(h, this);
        }
    }

    /// <inheritdoc cref="GetField(string)"/>
    public JsonValue this[string key] => GetField(key);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyDocument(Handle);
        Handle = 0;
    }
}
