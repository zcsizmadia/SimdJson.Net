using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
    private readonly JsonDocument? _owner;
    private bool _disposed;
    private readonly bool _isBorrowed;

    internal JsonValue(nint handle, JsonDocument? owner, bool isBorrowed = false)
    {
        Handle = handle;
        _owner = owner;
        _isBorrowed = isBorrowed;
    }

    /// <summary>
    /// Throws if this value, or the document that owns its native memory, has been disposed.
    /// Without the owner check, using a value after its document is gone would read freed memory.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owner is { IsDisposed: true })
        {
            throw new ObjectDisposedException(nameof(JsonDocument),
                "The JsonDocument that owns this value has been disposed.");
        }
    }

    /// <summary>Gets the JSON type of this value.</summary>
    public JsonValueKind ValueKind
    {
        get
        {
            ThrowIfDisposed();
            SimdJsonException.ThrowIfError(NativeMethods.ValueGetType(Handle, out var kind));
            return kind;
        }
    }

    /// <summary>Gets the value as a <see cref="string"/>. Throws if not a JSON string.</summary>
    public unsafe string GetString()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetString(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Gets the value as a <see cref="string"/>, replacing invalid UTF-8 sequences with U+FFFD instead of throwing.
    /// Throws if the value is not a JSON string.
    /// </summary>
    public unsafe string GetString(bool allowReplacement)
    {
        if (!allowReplacement)
        {
            return GetString();
        }

        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetStringAllowReplacement(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Gets the UTF-8 bytes of a JSON string value without allocating a managed string.
    /// The returned span is valid until this document is disposed.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetStringSpan()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetString(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>
    /// Gets the UTF-8 bytes of a JSON string value without allocating a managed string,
    /// replacing invalid UTF-8 sequences with U+FFFD bytes instead of throwing.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetStringSpan(bool allowReplacement)
    {
        if (!allowReplacement)
        {
            return GetStringSpan();
        }

        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetStringAllowReplacement(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>Gets the value as a <see cref="double"/>. Throws if not a number.</summary>
    public double GetDouble()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetDouble(Handle, out double v));
        return v;
    }

    /// <summary>Gets the value as an <see cref="long"/>. Throws if not an integer.</summary>
    public long GetInt64()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetInt64(Handle, out long v));
        return v;
    }

    /// <summary>Gets the value as a <see cref="ulong"/>. Throws if not an unsigned integer.</summary>
    public ulong GetUInt64()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetUInt64(Handle, out ulong v));
        return v;
    }

    /// <summary>Gets the value as a <see cref="bool"/>. Throws if not a boolean.</summary>
    public bool GetBool()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetBool(Handle, out int v));
        return v != 0;
    }

    /// <summary>Returns <see langword="true"/> if this value is a JSON null.</summary>
    public bool IsNull()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueIsNull(Handle, out int v));
        return v != 0;
    }

    /// <summary>Gets the value as a <see cref="JsonArray"/>. Throws if not an array.</summary>
    public JsonArray GetArray()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetArray(Handle, out var h));
        return new JsonArray(h, _owner!);
    }

    /// <summary>Gets the value as a <see cref="JsonObject"/>. Throws if not an object.</summary>
    public JsonObject GetObject()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetObject(Handle, out var h));
        return new JsonObject(h, _owner!);
    }

    // ── Convenience typed getters ────────────────────────────────────────────

    /// <summary>Gets the value as an <see cref="int"/>. Throws if not an integer, or if the value overflows <see cref="int"/>.</summary>
    public int GetInt32()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetInt32(Handle, out int v));
        return v;
    }

    /// <summary>Gets the value as a <see cref="uint"/>. Throws if not an integer, or if the value overflows <see cref="uint"/>.</summary>
    public uint GetUInt32()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetUInt32(Handle, out uint v));
        return v;
    }

    /// <summary>Gets the value as a <see cref="float"/>. Throws if not a number.</summary>
    public float GetFloat() => (float)GetDouble();

    /// <summary>
    /// Gets the value as a <see cref="decimal"/>, parsed from the raw JSON token so that
    /// values with more than 17 significant digits keep their precision.
    /// Throws <see cref="SimdJsonException"/> if the value is not a number or does not fit
    /// in a <see cref="decimal"/>.
    /// </summary>
    public decimal GetDecimal()
    {
        ThrowIfDisposed();
        if (ValueKind != JsonValueKind.Number)
        {
            throw new SimdJsonException(-2);
        }

        ReadOnlySpan<byte> token = GetRawJsonTokenSpan();
        if (!decimal.TryParse(token, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out decimal result))
        {
            throw new SimdJsonException(-10);
        }

        return result;
    }

    // ── Try-get value methods ────────────────────────────────────────────────

    /// <summary>Tries to get the value as a <see cref="string"/>. Returns <see langword="false"/> if the value is not a JSON string.</summary>
    public bool TryGetString(out string value)
    {
        try { value = GetString(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = null!; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="long"/>. Returns <see langword="false"/> if the value is not an integer.</summary>
    public bool TryGetInt64(out long value)
    {
        try { value = GetInt64(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="double"/>. Returns <see langword="false"/> if the value is not a number.</summary>
    public bool TryGetDouble(out double value)
    {
        try { value = GetDouble(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="float"/>. Returns <see langword="false"/> if the value is not a number.</summary>
    public bool TryGetFloat(out float value)
    {
        try { value = GetFloat(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="bool"/>. Returns <see langword="false"/> if the value is not a boolean.</summary>
    public bool TryGetBool(out bool value)
    {
        try { value = GetBool(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as an <see cref="int"/>. Returns <see langword="false"/> if the value is not an integer or overflows.</summary>
    public bool TryGetInt32(out int value)
    {
        try { value = GetInt32(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="uint"/>. Returns <see langword="false"/> if the value is not an integer or overflows.</summary>
    public bool TryGetUInt32(out uint value)
    {
        try { value = GetUInt32(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="ulong"/>. Returns <see langword="false"/> if the value is not an unsigned integer.</summary>
    public bool TryGetUInt64(out ulong value)
    {
        try { value = GetUInt64(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="JsonArray"/>. Returns <see langword="false"/> if the value is not an array.</summary>
    public bool TryGetArray(out JsonArray? value)
    {
        try { value = GetArray(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = null; return false; }
    }

    /// <summary>Tries to get the value as a <see cref="JsonObject"/>. Returns <see langword="false"/> if the value is not an object.</summary>
    public bool TryGetObject(out JsonObject? value)
    {
        try { value = GetObject(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsTypeMismatch(ex.ErrorCode)) { value = null; return false; }
    }

    /// <summary>Gets a child field by key. Throws if this value is not an object.</summary>
    public unsafe JsonValue GetField(string key)
    {
        ThrowIfDisposed();
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

    // ── Number type inspection ────────────────────────────────────────────────

    /// <summary>
    /// Gets the sub-type of a JSON number value (float / signed / unsigned / big integer).
    /// Throws if this value is not a number.
    /// </summary>
    public JsonNumberType GetNumberType()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetNumberType(Handle, out var t));
        return t;
    }

    /// <summary>
    /// Returns <see langword="true"/> if this number value is negative.
    /// Throws if this value is not a number.
    /// </summary>
    public bool IsNegative()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueIsNegative(Handle, out int v));
        return v != 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> if this number value has no fractional part.
    /// Throws if this value is not a number.
    /// </summary>
    public bool IsInteger()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueIsInteger(Handle, out int v));
        return v != 0;
    }

    // ── Raw JSON access ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw JSON token for a scalar value (quotes included for strings).
    /// For an array or object, returns only the opening bracket character.
    /// The returned string is decoded from the bytes that live in the document buffer.
    /// </summary>
    public unsafe string GetRawJsonToken()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueRawJsonToken(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> over the raw JSON token bytes without allocation.
    /// Valid only while this document is alive.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetRawJsonTokenSpan()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueRawJsonToken(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>
    /// Returns the full raw JSON of this value — traverses arrays and objects to find the end.
    /// Useful for extracting a JSON sub-document as a string.
    /// </summary>
    public unsafe string GetRawJson()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueRawJson(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    // ── Numbers encoded as strings ────────────────────────────────────────────

    /// <summary>
    /// Parses a <see cref="double"/> from a JSON string value like <c>"3.14"</c>.
    /// Throws if the value is not a string or the content is not a valid number.
    /// </summary>
    public double GetDoubleInString()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetDoubleInString(Handle, out double v));
        return v;
    }

    /// <summary>
    /// Parses an <see cref="long"/> from a JSON string value like <c>"-42"</c>.
    /// Throws if the value is not a string or the content is not a valid integer.
    /// </summary>
    public long GetInt64InString()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetInt64InString(Handle, out long v));
        return v;
    }

    /// <summary>
    /// Parses a <see cref="ulong"/> from a JSON string value like <c>"18446744073709551615"</c>.
    /// Throws if the value is not a string or the content is not a valid unsigned integer.
    /// </summary>
    public ulong GetUInt64InString()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetUInt64InString(Handle, out ulong v));
        return v;
    }

    /// <summary>Tries to parse a <see cref="double"/> from a JSON string value.</summary>
    public bool TryGetDoubleInString(out double value)
    {
        try { value = GetDoubleInString(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsValueMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to parse an <see cref="long"/> from a JSON string value.</summary>
    public bool TryGetInt64InString(out long value)
    {
        try { value = GetInt64InString(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsValueMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    /// <summary>Tries to parse a <see cref="ulong"/> from a JSON string value.</summary>
    public bool TryGetUInt64InString(out ulong value)
    {
        try { value = GetUInt64InString(); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsValueMismatch(ex.ErrorCode)) { value = default; return false; }
    }

    // ── JSON Pointer and JSONPath ──────────────────────────────────────────────

    /// <summary>
    /// Gets a descendant value via a JSON Pointer path (e.g. <c>"/items/0/name"</c>),
    /// starting from this value.
    /// </summary>
    public unsafe JsonValue AtPointer(string pointer)
    {
        ThrowIfDisposed();
        int maxBytes = Encoding.UTF8.GetMaxByteCount(pointer.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(pointer, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ValueAtPointer(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>
    /// Gets a descendant value via a JSONPath expression (e.g. <c>"$.items[0].name"</c>),
    /// starting from this value.
    /// </summary>
    public unsafe JsonValue AtPath(string jsonPath)
    {
        ThrowIfDisposed();
        int maxBytes = Encoding.UTF8.GetMaxByteCount(jsonPath.Length) + 1;
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(jsonPath, buf);
        buf[len] = 0;
        fixed (byte* p = buf)
        {
            SimdJsonException.ThrowIfError(NativeMethods.ValueAtPath(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>
    /// Searches forward from the current iterator position for a field by key without
    /// requiring fields to be in order (may rewind). Equivalent to <c>value::find_field_unordered()</c>.
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
            SimdJsonException.ThrowIfError(NativeMethods.ValueFindFieldUnordered(Handle, p, out var h));
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
    /// Searches forward from the current iterator position for a field with the given key
    /// (order-sensitive, does not rewind). Faster than <see cref="GetField"/> when fields
    /// are accessed in document order.
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
            SimdJsonException.ThrowIfError(NativeMethods.ValueFindField(Handle, p, out var h));
            return new JsonValue(h, _owner);
        }
    }

    /// <summary>Tries to get a descendant value via a JSON Pointer path.</summary>
    public bool TryAtPointer(string pointer, out JsonValue? value)
    {
        try { value = AtPointer(pointer); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { value = null; return false; }
    }

    /// <summary>Tries to get a descendant value via a JSONPath expression.</summary>
    public bool TryAtPath(string jsonPath, out JsonValue? value)
    {
        try { value = AtPath(jsonPath); return true; }
        catch (SimdJsonException ex) when (SimdJsonException.IsLookupMiss(ex.ErrorCode)) { value = null; return false; }
    }

    // ── Type predicates ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> if this value is a scalar (not array or object).
    /// </summary>
    public bool IsScalar()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueIsScalar(Handle, out int v));
        return v != 0;
    }

    /// <summary>Returns <see langword="true"/> if this value is a JSON string.</summary>
    public bool IsString()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueIsString(Handle, out int v));
        return v != 0;
    }

    // ── Parse location / depth ────────────────────────────────────────────────

    /// <summary>
    /// Returns the current parse position as a byte offset from the start of the
    /// document's JSON buffer. Useful for error messages ("error at byte N").
    /// </summary>
    /// <param name="owningDocument">
    /// The <see cref="JsonDocument"/> that owns this value (required to compute the offset).
    /// </param>
    public nuint CurrentOffset(JsonDocument owningDocument)
    {
        ArgumentNullException.ThrowIfNull(owningDocument);
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueCurrentOffset(Handle, owningDocument.Handle, out nuint offset));
        return offset;
    }

    /// <summary>Returns the current JSON nesting depth (0 = root level).</summary>
    public int CurrentDepth()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueCurrentDepth(Handle, out int depth));
        return depth;
    }

    // ── Structured number ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full typed number value in a single call as a <see cref="JsonNumber"/>.
    /// Avoids the need to call <see cref="GetNumberType"/> and a separate getter.
    /// For <see cref="JsonNumberType.BigInteger"/> numbers, use <see cref="GetRawJsonToken"/>
    /// to retrieve the decimal string representation.
    /// </summary>
    public JsonNumber GetNumber()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetNumber(Handle, out var n));
        return new JsonNumber(
            (JsonNumberType)n.Type,
            n.FloatingPoint,
            n.SignedInteger,
            n.UnsignedInteger);
    }

    // ── Wobbly / WTF-8 string ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the string value allowing lone Unicode surrogates (WTF-8 / CESU-8).
    /// The returned bytes may not be valid UTF-8. Use this for round-tripping JSON
    /// produced by runtimes (e.g. Java) that emit lone surrogates.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetWobblyStringSpan()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetWobblyString(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    public void Dispose()
    {
        if (_disposed || _isBorrowed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.DestroyValue(Handle);
        Handle = 0;
    }

    // ── Raw JSON string (without unescaping) ──────────────────────────────────

    /// <summary>
    /// Returns the raw (still-escaped) UTF-8 bytes of a JSON string value, without the
    /// surrounding quote characters.
    /// For example, for <c>"hello\nworld"</c> this returns the bytes
    /// <c>h e l l o \ n w o r l d</c> (backslash + n, not a newline).
    /// The span is valid for the lifetime of the owning document.
    /// Throws if this value is not a JSON string.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetRawJsonStringSpan()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetRawJsonString(Handle, out byte* ptr, out nuint len));
        return new ReadOnlySpan<byte>(ptr, (int)len);
    }

    /// <summary>
    /// Returns the raw (still-escaped) bytes of a JSON string value as a <see cref="string"/>,
    /// without the surrounding quote characters.
    /// Throws if this value is not a JSON string.
    /// </summary>
    public unsafe string GetRawJsonString()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueGetRawJsonString(Handle, out byte* ptr, out nuint len));
        return Encoding.UTF8.GetString(ptr, (int)len);
    }

    // ── Wildcard path iteration ───────────────────────────────────────────────

    /// <summary>
    /// Returns the number of elements when this value is an array (requires a full scan).
    /// Throws if this value is not an array.
    /// </summary>
    public int CountElements()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueCountElements(Handle, out nuint n));
        return (int)n;
    }

    /// <summary>
    /// Returns the number of fields when this value is an object (requires a full scan).
    /// Throws if this value is not an object.
    /// </summary>
    public int CountFields()
    {
        ThrowIfDisposed();
        SimdJsonException.ThrowIfError(NativeMethods.ValueCountFields(Handle, out nuint n));
        return (int)n;
    }

    /// <summary>
    /// Iterates over all values in this value (which must be an array or object) that match
    /// the given JSONPath expression with wildcard support (e.g. <c>"$[*]"</c>,
    /// <c>"$.items[*].name"</c>) and invokes <paramref name="callback"/> for each match.
    /// </summary>
    /// <remarks>
    /// The <see cref="JsonValue"/> passed to <paramref name="callback"/> is borrowed —
    /// it is valid only for the duration of the callback invocation and must not be disposed
    /// or stored for use after the callback returns.
    /// </remarks>
    public unsafe void ForEachAtPath(string path, Action<JsonValue> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(callback);
        int maxBytes = Encoding.UTF8.GetMaxByteCount(path.Length);
        Span<byte> buf = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int len = Encoding.UTF8.GetBytes(path, buf);
        var context = new WildcardContext(callback);
        var gcHandle = GCHandle.Alloc(context);
        try
        {
            int err;
            fixed (byte* p = buf)
            {
                err = NativeMethods.ValueForEachAtPath(
                    Handle, p, (nuint)len, s_wildcardTrampolinePtr, GCHandle.ToIntPtr(gcHandle));
            }
            context.Rethrow();
            SimdJsonException.ThrowIfError(err);
        }
        finally { gcHandle.Free(); }
    }

    // ── Internal wildcard trampoline ──────────────────────────────────────────

    /// <summary>
    /// Carries the user callback across the native call and captures any exception it throws.
    /// An exception must not propagate out of the <see cref="UnmanagedCallersOnlyAttribute"/>
    /// frame below — the runtime would terminate the process — so it is stored here and
    /// rethrown by the caller once the native iteration has returned.
    /// </summary>
    internal sealed class WildcardContext(Action<JsonValue> callback)
    {
        private readonly Action<JsonValue> _callback = callback;
        private ExceptionDispatchInfo? _fault;

        internal void Invoke(JsonValue value)
        {
            if (_fault is not null)
            {
                return;
            }

            try
            {
                _callback(value);
            }
            catch (Exception ex)
            {
                _fault = ExceptionDispatchInfo.Capture(ex);
            }
        }

        internal void Rethrow() => _fault?.Throw();
    }

    internal static readonly unsafe nint s_wildcardTrampolinePtr =
        (nint)(delegate* unmanaged[Cdecl]<nint, nint, void>)&WildcardTrampoline;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WildcardTrampoline(nint valueHandle, nint context)
    {
        var ctx = (WildcardContext)GCHandle.FromIntPtr(context).Target!;
        var tmp = new JsonValue(valueHandle, null, isBorrowed: true);
        ctx.Invoke(tmp);
        // The native BridgeValue lives on the C++ stack for the duration of this call only.
        // Poison the wrapper so a callback that stored it gets ObjectDisposedException
        // instead of dereferencing a dead pointer.
        tmp.Handle = 0;
        tmp._disposed = true;
    }
}
