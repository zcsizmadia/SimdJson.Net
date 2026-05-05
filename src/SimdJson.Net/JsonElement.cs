using System.Runtime.CompilerServices;
using System.Text;

namespace SimdJson.Net;

/// <summary>
/// A handle into a parsed <see cref="JsonDocument"/>. Cheap to copy
/// (a reference plus an integer). All accessors are zero-allocation
/// except where transcoding to <see cref="string"/> is explicitly requested.
/// </summary>
public readonly struct JsonElement
{
    private readonly JsonDocument _doc;
    private readonly int _tapeIndex;

    internal JsonElement(JsonDocument doc, int tapeIndex)
    {
        _doc = doc;
        _tapeIndex = tapeIndex;
    }

    private long Entry => _doc.Tape[_tapeIndex];

    /// <summary>The kind of JSON value this element represents.</summary>
    public JsonElementType ValueKind => (JsonElementType)(byte)(Entry >> 56);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Payload(long entry) => entry & 0x00FF_FFFF_FFFF_FFFFL;

    // ---- Scalars --------------------------------------------------------

    public bool GetBoolean() => ValueKind switch
    {
        JsonElementType.True  => true,
        JsonElementType.False => false,
        _ => throw new InvalidOperationException($"Element is {ValueKind}, not boolean."),
    };

    public long GetInt64()
    {
        if (ValueKind != JsonElementType.Int64)
            throw new InvalidOperationException($"Element is {ValueKind}, not Int64.");
        return _doc.Tape[_tapeIndex + 1];
    }

    public double GetDouble()
    {
        return ValueKind switch
        {
            JsonElementType.Double => BitConverter.Int64BitsToDouble(_doc.Tape[_tapeIndex + 1]),
            JsonElementType.Int64  => _doc.Tape[_tapeIndex + 1],
            _ => throw new InvalidOperationException($"Element is {ValueKind}, not numeric."),
        };
    }

    /// <summary>Returns the raw UTF-8 bytes of a string element (no allocation).</summary>
    public ReadOnlySpan<byte> GetUtf8Span()
    {
        if (ValueKind != JsonElementType.String)
            throw new InvalidOperationException($"Element is {ValueKind}, not String.");
        long pl = Payload(Entry);
        int offset = (int)(pl & 0xFFFFFFFF);
        int length = (int)(pl >> 32);
        return _doc.StringBuffer.Slice(offset, length);
    }

    public string GetString() => ValueKind == JsonElementType.Null
        ? null!
        : Encoding.UTF8.GetString(GetUtf8Span());

    // ---- Containers -----------------------------------------------------

    /// <summary>Number of elements in an array, or key/value pairs in an object.</summary>
    public int GetLength()
    {
        switch (ValueKind)
        {
            case JsonElementType.Array:
            case JsonElementType.Object:
                int end = (int)Payload(Entry);
                return CountChildren(_tapeIndex + 1, end);
            case JsonElementType.String:
                return GetUtf8Span().Length;
            default:
                throw new InvalidOperationException($"Element is {ValueKind}; no length.");
        }
    }

    private int CountChildren(int from, int to)
    {
        int count = 0;
        int i = from;
        while (i < to)
        {
            count++;
            i = SkipValue(i);
        }
        if (ValueKind == JsonElementType.Object) count /= 2; // key+value pairs
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SkipValue(int i)
    {
        long e = _doc.Tape[i];
        var kind = (JsonElementType)(byte)(e >> 56);
        return kind switch
        {
            JsonElementType.Object or JsonElementType.Array => (int)Payload(e),
            JsonElementType.Int64 or JsonElementType.Double => i + 2,
            _ => i + 1,
        };
    }

    /// <summary>Indexer over an array.</summary>
    public JsonElement this[int index]
    {
        get
        {
            if (ValueKind != JsonElementType.Array)
                throw new InvalidOperationException("Indexer requires an array.");
            int end = (int)Payload(Entry);
            int i = _tapeIndex + 1;
            int n = 0;
            while (i < end)
            {
                if (n == index) return new JsonElement(_doc, i);
                i = SkipValue(i);
                n++;
            }
            throw new IndexOutOfRangeException();
        }
    }

    /// <summary>Look up a property by name on an object.</summary>
    public JsonElement this[ReadOnlySpan<byte> key]
    {
        get
        {
            if (TryGetProperty(key, out var v)) return v;
            throw new KeyNotFoundException();
        }
    }

    public JsonElement this[string key] => this[Encoding.UTF8.GetBytes(key).AsSpan()];

    public bool TryGetProperty(ReadOnlySpan<byte> key, out JsonElement value)
    {
        if (ValueKind != JsonElementType.Object)
            throw new InvalidOperationException("Property lookup requires an object.");
        int end = (int)Payload(Entry);
        int i = _tapeIndex + 1;
        while (i < end)
        {
            // Key must be a string entry
            var keyEl = new JsonElement(_doc, i);
            if (keyEl.ValueKind != JsonElementType.String)
                throw new SimdJsonException("Object key is not a string (corrupt tape).");
            i++; // strings are one slot
            var valEl = new JsonElement(_doc, i);
            i = SkipValue(i);
            if (keyEl.GetUtf8Span().SequenceEqual(key))
            {
                value = valEl;
                return true;
            }
        }
        value = default;
        return false;
    }

    public bool TryGetProperty(string key, out JsonElement value)
    {
        Span<byte> buf = key.Length <= 256
            ? stackalloc byte[Encoding.UTF8.GetMaxByteCount(key.Length)]
            : new byte[Encoding.UTF8.GetMaxByteCount(key.Length)];
        int n = Encoding.UTF8.GetBytes(key, buf);
        return TryGetProperty(buf[..n], out value);
    }

    // ---- Enumeration ----------------------------------------------------

    public ArrayEnumerator EnumerateArray()
    {
        if (ValueKind != JsonElementType.Array)
            throw new InvalidOperationException("Element is not an array.");
        return new ArrayEnumerator(_doc, _tapeIndex + 1, (int)Payload(Entry));
    }

    public ObjectEnumerator EnumerateObject()
    {
        if (ValueKind != JsonElementType.Object)
            throw new InvalidOperationException("Element is not an object.");
        return new ObjectEnumerator(_doc, _tapeIndex + 1, (int)Payload(Entry));
    }

    public struct ArrayEnumerator
    {
        private readonly JsonDocument _doc;
        private int _i;
        private readonly int _end;
        private JsonElement _current;
        internal ArrayEnumerator(JsonDocument doc, int start, int end)
        { _doc = doc; _i = start; _end = end; _current = default; }
        public JsonElement Current => _current;
        public bool MoveNext()
        {
            if (_i >= _end) return false;
            _current = new JsonElement(_doc, _i);
            long e = _doc.Tape[_i];
            var k = (JsonElementType)(byte)(e >> 56);
            _i = k switch
            {
                JsonElementType.Object or JsonElementType.Array => (int)(e & 0x00FF_FFFF_FFFF_FFFFL),
                JsonElementType.Int64 or JsonElementType.Double => _i + 2,
                _ => _i + 1,
            };
            return true;
        }
        public ArrayEnumerator GetEnumerator() => this;
    }

    public struct ObjectEnumerator
    {
        private readonly JsonDocument _doc;
        private int _i;
        private readonly int _end;
        private JsonProperty _current;
        internal ObjectEnumerator(JsonDocument doc, int start, int end)
        { _doc = doc; _i = start; _end = end; _current = default; }
        public JsonProperty Current => _current;
        public bool MoveNext()
        {
            if (_i >= _end) return false;
            var key = new JsonElement(_doc, _i);
            _i++; // string key occupies one slot
            var val = new JsonElement(_doc, _i);
            long e = _doc.Tape[_i];
            var k = (JsonElementType)(byte)(e >> 56);
            _i = k switch
            {
                JsonElementType.Object or JsonElementType.Array => (int)(e & 0x00FF_FFFF_FFFF_FFFFL),
                JsonElementType.Int64 or JsonElementType.Double => _i + 2,
                _ => _i + 1,
            };
            _current = new JsonProperty(key, val);
            return true;
        }
        public ObjectEnumerator GetEnumerator() => this;
    }
}

/// <summary>A key/value pair from an object enumeration.</summary>
public readonly struct JsonProperty
{
    public JsonElement Name { get; }
    public JsonElement Value { get; }
    public JsonProperty(JsonElement name, JsonElement value) { Name = name; Value = value; }
    public ReadOnlySpan<byte> NameUtf8 => Name.GetUtf8Span();
}
