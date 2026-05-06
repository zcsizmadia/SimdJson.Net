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

    /// <summary>
    /// Tries to get a field by key.
    /// Returns <see langword="false"/> (and <see langword="null"/>) if the key does not exist.
    /// </summary>
    public bool TryGetField(string key, out JsonValue? value)
    {
        try { value = GetField(key); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>Returns <see langword="true"/> if a field with the given key exists.</summary>
    public bool ContainsKey(string key)
    {
        try { using var v = GetField(key); return true; }
        catch (SimdJsonException) { return false; }
    }

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
                if (done)
                {
                    yield break;
                }

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

    /// <summary>Gets a value via a JSON Pointer path (e.g. <c>"/address/city"</c>).</summary>
    public unsafe JsonValue AtPointer(string pointer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(pointer.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(pointer, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ObjectAtPointer(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>Gets a value via a JSONPath expression (e.g. <c>"$.address.city"</c>).</summary>
    public unsafe JsonValue AtPath(string jsonPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(jsonPath.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(jsonPath, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ObjectAtPath(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>
    /// Searches forward from the current position for a field with the given key
    /// (order-sensitive). Faster than <see cref="GetField"/> when accessing fields in order.
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
            SimdJsonException.ThrowIfError(NativeMethods.ObjectFindField(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>Returns <see langword="true"/> if the object has no fields.</summary>
    public bool IsEmpty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ObjectIsEmpty(Handle, out int v));
        return v != 0;
    }

    /// <summary>
    /// Searches for a field by key without requiring fields to appear in order
    /// (may rewind the iterator). Useful when field order is unknown.
    /// </summary>
    public unsafe JsonValue FindFieldUnordered(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(key, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ObjectFindFieldUnordered(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>Tries to find a field unordered; returns <see langword="false"/> if not found.</summary>
    public bool TryFindFieldUnordered(string key, out JsonValue? value)
    {
        try { value = FindFieldUnordered(key); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>
    /// Iterates over all values in this object that match the given JSONPath wildcard expression
    /// (e.g. <c>"$.*"</c>, <c>"$.items[*].name"</c>) and invokes <paramref name="callback"/> for each match.
    /// </summary>
    /// <remarks>
    /// The <see cref="JsonValue"/> passed to <paramref name="callback"/> is borrowed —
    /// valid only during the callback, must not be disposed or stored.
    /// </remarks>
    public unsafe void ForEachAtPath(string path, Action<JsonValue> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(path.Length);
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(path, buf);
        var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(callback);
        try
        {
            fixed (byte* p = buf)
            {
                SimdJsonException.ThrowIfError(NativeMethods.ObjectForEachAtPath(
                    Handle, p, (nuint)len, JsonValue.s_wildcardTrampolinePtr,
                    System.Runtime.InteropServices.GCHandle.ToIntPtr(gcHandle)));
            }
        }
        finally { gcHandle.Free(); }
    }

    /// <summary>Tries to get a value via a JSON Pointer path.</summary>
    public bool TryAtPointer(string pointer, out JsonValue? value)
    {
        try { value = AtPointer(pointer); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>Tries to get a value via a JSONPath expression.</summary>
    public bool TryAtPath(string jsonPath, out JsonValue? value)
    {
        try { value = AtPath(jsonPath); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>
    /// Returns the full raw JSON of this object as a string.
    /// This operation consumes the object iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe string GetRawJson()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ObjectRawJson(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Returns the full raw JSON of this object as a <see cref="ReadOnlySpan{T}"/> without allocating a string.
    /// This operation consumes the object iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetRawJsonSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ObjectRawJson(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>Resets the object iterator so the object can be traversed again.</summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ObjectReset(Handle));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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
