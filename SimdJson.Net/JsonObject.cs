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
    /// Throws if this object, or the document that owns its native memory, has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owner is { IsDisposed: true })
        {
            throw new ObjectDisposedException(nameof(JsonDocument),
                "The JsonDocument that owns this object has been disposed.");
        }
    }

    /// <summary>
    /// Returns the number of fields (requires a full native scan — use sparingly).
    /// </summary>
    public int Count
    {
        get
        {
            ThrowIfDisposed();
            SimdJsonException.ThrowIfError(NativeMethods.ObjectCount(Handle, out nuint n));
            return (int)n;
        }
    }

    /// <summary>Gets a field by key (order-insensitive lookup).</summary>
    public unsafe JsonValue GetField(string key)
    {
        ThrowIfDisposed();
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
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { value = null; return false; }
    }

    /// <summary>Returns <see langword="true"/> if a field with the given key exists.</summary>
    public bool ContainsKey(string key)
    {
        try { using var v = GetField(key); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { return false; }
    }

    /// <summary>Iterates over all key-value pairs.</summary>
    public IEnumerator<JsonProperty> GetEnumerator()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ObjectBegin(Handle, out nint iter));
        try
        {
            while (true)
            {
                NextField(iter, out string? key, out nint escapedKey, out int escapedKeyLength,
                    out nint valHandle, out bool done);
                if (done)
                {
                    yield break;
                }

                yield return new JsonProperty(
                    key!, new JsonValue(valHandle, _owner), escapedKey, escapedKeyLength);
            }
        }
        finally
        {
            NativeMethods.DestroyObjectIter(iter);
        }
    }

    private static unsafe void NextField(
        nint iter, out string? key, out nint escapedKey, out int escapedKeyLength,
        out nint valHandle, out bool done)
    {
        SimdJsonException.ThrowIfError(
            NativeMethods.ObjectIterNext(iter,
                out byte* keyPtr, out nuint keyLen,
                out byte* escapedPtr, out nuint escapedLen,
                out valHandle,
                out int doneInt));
        done = doneInt != 0;
        key = done ? null : System.Text.Encoding.UTF8.GetString(keyPtr, (int)keyLen);
        escapedKey = (nint)escapedPtr;
        escapedKeyLength = (int)escapedLen;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Gets a value via a JSON Pointer path (e.g. <c>"/address/city"</c>).</summary>
    public unsafe JsonValue AtPointer(string pointer)
    {
        ThrowIfDisposed();
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
        ThrowIfDisposed();
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
        ThrowIfDisposed();
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
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ObjectIsEmpty(Handle, out int v));
        return v != 0;
    }

    /// <summary>
    /// Searches for a field by key without requiring fields to appear in order
    /// (may rewind the iterator). Useful when field order is unknown.
    /// </summary>
    public unsafe JsonValue FindFieldUnordered(string key)
    {
        ThrowIfDisposed();
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
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { value = null; return false; }
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(callback);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(path.Length);
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(path, buf);
        var context = new JsonValue.WildcardContext(callback);
        var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(context);
        try
        {
            int err;
            fixed (byte* p = buf)
            {
                err = NativeMethods.ObjectForEachAtPath(
                    Handle, p, (nuint)len, JsonValue.s_wildcardTrampolinePtr,
                    System.Runtime.InteropServices.GCHandle.ToIntPtr(gcHandle));
            }
            context.Rethrow();
            SimdJsonException.ThrowIfError(err);
        }
        finally { gcHandle.Free(); }
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
    /// Returns the full raw JSON of this object as a string.
    /// This operation consumes the object iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe string GetRawJson()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ObjectRawJson(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Returns the full raw JSON of this object as a <see cref="ReadOnlySpan{T}"/> without allocating a string.
    /// This operation consumes the object iterator; call <see cref="Reset"/> to iterate again.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetRawJsonSpan()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ObjectRawJson(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>Resets the object iterator so the object can be traversed again.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
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
public readonly struct JsonProperty
{
    private readonly nint _escapedName;
    private readonly int _escapedNameLength;

    internal JsonProperty(string name, JsonValue value, nint escapedName, int escapedNameLength)
    {
        Name = name;
        Value = value;
        _escapedName = escapedName;
        _escapedNameLength = escapedNameLength;
    }

    /// <summary>The field name, with JSON escape sequences resolved.</summary>
    public string Name { get; }

    /// <summary>The field value. Dispose when no longer needed.</summary>
    public JsonValue Value { get; }

    /// <summary>
    /// The field name exactly as it appears in the JSON text, with escape sequences left intact,
    /// as UTF-8 bytes and without the surrounding quotes.
    /// </summary>
    /// <remarks>
    /// This is the form that <see cref="JsonObject.GetField"/>, <see cref="JsonObject.FindField"/>
    /// and the pointer lookups compare against, so it is what you pass back to look the field up
    /// again. For a key with no escape sequences it is the same text as <see cref="Name"/>.
    /// <para>
    /// The span points into the document buffer and is valid until the owning document is
    /// disposed. It is not invalidated by advancing the enumerator, unlike <see cref="Value"/>.
    /// </para>
    /// </remarks>
    public unsafe ReadOnlySpan<byte> EscapedNameSpan =>
        _escapedName == 0 ? default : new ReadOnlySpan<byte>((byte*)_escapedName, _escapedNameLength);

    /// <summary>
    /// The field name exactly as it appears in the JSON text, with escape sequences left intact.
    /// Allocates; prefer <see cref="EscapedNameSpan"/> on hot paths.
    /// </summary>
    public unsafe string EscapedName =>
        _escapedName == 0
            ? Name
            : System.Text.Encoding.UTF8.GetString((byte*)_escapedName, _escapedNameLength);
}
