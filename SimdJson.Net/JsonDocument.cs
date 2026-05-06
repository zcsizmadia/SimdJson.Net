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

    /// <summary>
    /// Tries to get a field from the root object by key.
    /// Returns <see langword="false"/> (and <see langword="null"/>) if the field does not exist.
    /// </summary>
    public bool TryGetField(string key, out JsonValue? value)
    {
        try { value = GetField(key); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>
    /// Tries to get a value at a JSON Pointer path.
    /// Returns <see langword="false"/> (and <see langword="null"/>) if the path does not resolve.
    /// </summary>
    public bool TryAtPointer(string pointer, out JsonValue? value)
    {
        try { value = AtPointer(pointer); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>Gets a value via a JSONPath expression (e.g. <c>"$.items[0].name"</c>).</summary>
    public unsafe JsonValue AtPath(string jsonPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(jsonPath.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(jsonPath, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.DocumentAtPath(Handle, p, out var h));
            return new JsonValue(h, this);
        }
    }

    /// <summary>Tries to get a value via a JSONPath expression.</summary>
    public bool TryAtPath(string jsonPath, out JsonValue? value)
    {
        try { value = AtPath(jsonPath); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>
    /// Searches forward from the current iterator position for a field with the given key
    /// (order-sensitive, does not rewind). Faster than <see cref="GetField"/> when fields
    /// are accessed in document order.
    /// </summary>
    public unsafe JsonValue FindField(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(key, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.DocumentFindField(Handle, p, out var h));
            return new JsonValue(h, this);
        }
    }

    /// <summary>
    /// Rewinds the document iterator to the start, allowing the document to be re-read.
    /// Note: existing <see cref="JsonValue"/>, <see cref="JsonArray"/>, and
    /// <see cref="JsonObject"/> handles obtained before rewind become invalid.
    /// </summary>
    public void Rewind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.DocumentRewind(Handle));
    }

    /// <summary>Returns the full raw JSON of the document root as a string.</summary>
    public unsafe string GetRawJson()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.DocumentRawJson(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyDocument(Handle);
        Handle = 0;
    }
}
