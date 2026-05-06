using System.Text;
using SimdJson.Internal;

namespace SimdJson;

/// <summary>
/// A JSON value obtained from a parsed document.
/// The backing memory is owned by the <see cref="JsonDocument"/>; disposing the document
/// invalidates this instance. Dispose this value when no longer needed to free the native handle.
/// </summary>
public sealed class JsonValue : IDisposable
{
    internal nint Handle;
    private readonly JsonDocument _owner;
    private bool _disposed;

    internal JsonValue(nint handle, JsonDocument owner)
    {
        Handle = handle;
        _owner = owner;
    }

    /// <summary>Gets the JSON type of this value.</summary>
    public JsonValueKind ValueKind
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SimdJsonException.ThrowIfError(NativeMethods.ValueGetType(Handle, out var kind));
            return kind;
        }
    }

    /// <summary>Gets the value as a <see cref="string"/>. Throws if not a JSON string.</summary>
    public unsafe string GetString()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetString(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Gets the UTF-8 bytes of a JSON string value without allocating a managed string.
    /// The returned span is valid until this document is disposed.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetStringSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetString(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>Gets the value as a <see cref="double"/>. Throws if not a number.</summary>
    public double GetDouble()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetDouble(Handle, out double v));
        return v;
    }

    /// <summary>Gets the value as an <see cref="long"/>. Throws if not an integer.</summary>
    public long GetInt64()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetInt64(Handle, out long v));
        return v;
    }

    /// <summary>Gets the value as a <see cref="ulong"/>. Throws if not an unsigned integer.</summary>
    public ulong GetUInt64()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetUInt64(Handle, out ulong v));
        return v;
    }

    /// <summary>Gets the value as a <see cref="bool"/>. Throws if not a boolean.</summary>
    public bool GetBool()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetBool(Handle, out int v));
        return v != 0;
    }

    /// <summary>Returns <see langword="true"/> if this value is a JSON null.</summary>
    public bool IsNull()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueIsNull(Handle, out int v));
        return v != 0;
    }

    /// <summary>Gets the value as a <see cref="JsonArray"/>. Throws if not an array.</summary>
    public JsonArray GetArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetArray(Handle, out var h));
        return new JsonArray(h, _owner);
    }

    /// <summary>Gets the value as a <see cref="JsonObject"/>. Throws if not an object.</summary>
    public JsonObject GetObject()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetObject(Handle, out var h));
        return new JsonObject(h, _owner);
    }

    // ── Convenience typed getters ────────────────────────────────────────────

    /// <summary>Gets the value as an <see cref="int"/>. Throws if not an integer.</summary>
    public int GetInt32() => (int)GetInt64();

    /// <summary>Gets the value as a <see cref="float"/>. Throws if not a number.</summary>
    public float GetFloat() => (float)GetDouble();

    /// <summary>Gets the value as a <see cref="decimal"/>. Throws if not a number.</summary>
    public decimal GetDecimal() => (decimal)GetDouble();

    // ── Try-get value methods ────────────────────────────────────────────────

    /// <summary>Tries to get the value as a <see cref="string"/>. Returns <see langword="false"/> if the value is not a JSON string.</summary>
    public bool TryGetString(out string value)
    {
        try { value = GetString(); return true; }
        catch (SimdJsonException) { value = null!; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="long"/>. Returns <see langword="false"/> if the value is not an integer.</summary>
    public bool TryGetInt64(out long value)
    {
        try { value = GetInt64(); return true; }
        catch (SimdJsonException) { value = default; return false; }
    }

    /// <summary>Tries to get the value as an <see cref="int"/>. Returns <see langword="false"/> if the value is not an integer.</summary>
    public bool TryGetInt32(out int value)
    {
        try { value = GetInt32(); return true; }
        catch (SimdJsonException) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="double"/>. Returns <see langword="false"/> if the value is not a number.</summary>
    public bool TryGetDouble(out double value)
    {
        try { value = GetDouble(); return true; }
        catch (SimdJsonException) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="float"/>. Returns <see langword="false"/> if the value is not a number.</summary>
    public bool TryGetFloat(out float value)
    {
        try { value = GetFloat(); return true; }
        catch (SimdJsonException) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="bool"/>. Returns <see langword="false"/> if the value is not a boolean.</summary>
    public bool TryGetBool(out bool value)
    {
        try { value = GetBool(); return true; }
        catch (SimdJsonException) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="ulong"/>. Returns <see langword="false"/> if the value is not an unsigned integer.</summary>
    public bool TryGetUInt64(out ulong value)
    {
        try { value = GetUInt64(); return true; }
        catch (SimdJsonException) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="JsonArray"/>. Returns <see langword="false"/> if the value is not an array.</summary>
    public bool TryGetArray(out JsonArray? value)
    {
        try { value = GetArray(); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="JsonObject"/>. Returns <see langword="false"/> if the value is not an object.</summary>
    public bool TryGetObject(out JsonObject? value)
    {
        try { value = GetObject(); return true; }
        catch (SimdJsonException) { value = null; return false; }
    }

    /// <summary>Gets a child field by key. Throws if this value is not an object.</summary>
    public unsafe JsonValue GetField(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(key, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ValueGetFieldByKey(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <inheritdoc cref="GetField(string)"/>
    public JsonValue this[string key] => GetField(key);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.DestroyValue(Handle);
        Handle = 0;
    }
}
