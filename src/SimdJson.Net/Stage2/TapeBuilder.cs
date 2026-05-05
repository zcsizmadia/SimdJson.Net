using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace SimdJson.Net.Stage2;

/// <summary>
/// Encodes the parsed JSON value tree into a flat <c>long[]</c> "tape" — the
/// same data layout used by the original simdjson C++ library. This format
/// is extremely cache-friendly: container traversal is a sequential scan,
/// and downstream consumers (a future parquet writer, a System.Text.Json
/// replacement, etc.) can walk the tape without allocating intermediate
/// node objects.
/// <para>
/// Tape entry layout:
/// <list type="bullet">
///   <item><c>tape[i] &amp; 0x00FFFFFFFFFFFFFF</c> — payload (count, offset, child index, value)</item>
///   <item><c>(byte)(tape[i] &gt;&gt; 56)</c> — <see cref="JsonElementType"/> tag</item>
/// </list>
/// Numbers occupy two tape slots (tag + raw bit pattern). Strings store an
/// offset into the document's string buffer and a length.
/// </para>
/// </summary>
internal sealed class TapeBuilder
{
    private readonly byte[] _input;
    private readonly int _inputLength;
    private readonly int[] _structurals;
    private readonly int _structuralCount;

    private long[] _tape;
    private int _tapeIndex;

    private byte[] _stringBuffer;
    private int _stringBufferIndex;

    public TapeBuilder(byte[] input, int inputLength, int[] structurals, int structuralCount)
    {
        _input = input;
        _inputLength = inputLength;
        _structurals = structurals;
        _structuralCount = structuralCount;
        // Tape needs at most ~1.1x structural count slots (numbers take 2).
        _tape = ArrayPool<long>.Shared.Rent(Math.Max(16, structuralCount + (structuralCount >> 2) + 16));
        // Decoded strings are at most as long as the raw input (escapes shrink).
        _stringBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(64, inputLength + 32));
    }

    public long[] Tape => _tape;
    public int TapeLength => _tapeIndex;
    public byte[] StringBuffer => _stringBuffer;
    public int StringBufferLength => _stringBufferIndex;

    public void Build()
    {
        if (_structuralCount == 0)
            throw SimdJsonException.Truncated();

        int idx = 0;
        ParseValue(ref idx, depth: 0);

        // Skip trailing whitespace-only structurals (none should remain)
        if (idx != _structuralCount)
        {
            int pos = _structurals[idx];
            throw SimdJsonException.Unexpected(_input[pos], pos);
        }
    }

    // --- Tape primitives -------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureTape(int extra)
    {
        if (_tapeIndex + extra > _tape.Length)
        {
            var bigger = ArrayPool<long>.Shared.Rent(Math.Max(_tape.Length * 2, _tapeIndex + extra));
            Array.Copy(_tape, bigger, _tapeIndex);
            ArrayPool<long>.Shared.Return(_tape);
            _tape = bigger;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureStringBuffer(int extra)
    {
        if (_stringBufferIndex + extra > _stringBuffer.Length)
        {
            var bigger = ArrayPool<byte>.Shared.Rent(Math.Max(_stringBuffer.Length * 2, _stringBufferIndex + extra));
            Array.Copy(_stringBuffer, bigger, _stringBufferIndex);
            ArrayPool<byte>.Shared.Return(_stringBuffer);
            _stringBuffer = bigger;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Encode(JsonElementType tag, long payload)
        => ((long)(byte)tag << 56) | (payload & 0x00FF_FFFF_FFFF_FFFFL);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(JsonElementType tag, long payload)
    {
        EnsureTape(1);
        _tape[_tapeIndex++] = Encode(tag, payload);
    }

    // --- Recursive descent over structural indices -----------------------

    private void ParseValue(ref int idx, int depth)
    {
        if (depth > 1024)
            throw new SimdJsonException("Maximum nesting depth exceeded.");

        if (idx >= _structuralCount) throw SimdJsonException.Truncated();
        int pos = _structurals[idx];
        byte b = _input[pos];

        switch (b)
        {
            case (byte)'{': ParseObject(ref idx, depth); break;
            case (byte)'[': ParseArray(ref idx, depth); break;
            case (byte)'"': ParseString(ref idx); break;
            case (byte)'t': ParseLiteral(ref idx, "true"u8, JsonElementType.True); break;
            case (byte)'f': ParseLiteral(ref idx, "false"u8, JsonElementType.False); break;
            case (byte)'n': ParseLiteral(ref idx, "null"u8, JsonElementType.Null); break;
            case (byte)'-':
            case >= (byte)'0' and <= (byte)'9':
                ParseNumber(ref idx);
                break;
            default:
                throw SimdJsonException.Unexpected(b, pos);
        }
    }

    private void ParseObject(ref int idx, int depth)
    {
        int startTape = _tapeIndex;
        Write(JsonElementType.Object, 0); // payload patched at end with end-tape index
        idx++; // consume '{'

        if (idx >= _structuralCount) throw SimdJsonException.Truncated();
        if (_input[_structurals[idx]] == (byte)'}')
        {
            idx++;
            // empty object: payload = self+1 (no children)
            _tape[startTape] = Encode(JsonElementType.Object, _tapeIndex);
            return;
        }

        while (true)
        {
            // Key MUST be a string
            int pos = _structurals[idx];
            if (_input[pos] != (byte)'"') throw SimdJsonException.Unexpected(_input[pos], pos);
            ParseString(ref idx);

            // Colon
            if (idx >= _structuralCount) throw SimdJsonException.Truncated();
            pos = _structurals[idx];
            if (_input[pos] != (byte)':') throw SimdJsonException.Unexpected(_input[pos], pos);
            idx++;

            // Value
            ParseValue(ref idx, depth + 1);

            // Comma or close
            if (idx >= _structuralCount) throw SimdJsonException.Truncated();
            pos = _structurals[idx];
            byte sep = _input[pos];
            idx++;
            if (sep == (byte)'}') break;
            if (sep != (byte)',') throw SimdJsonException.Unexpected(sep, pos);
        }

        _tape[startTape] = Encode(JsonElementType.Object, _tapeIndex);
    }

    private void ParseArray(ref int idx, int depth)
    {
        int startTape = _tapeIndex;
        Write(JsonElementType.Array, 0);
        idx++;

        if (idx >= _structuralCount) throw SimdJsonException.Truncated();
        if (_input[_structurals[idx]] == (byte)']')
        {
            idx++;
            _tape[startTape] = Encode(JsonElementType.Array, _tapeIndex);
            return;
        }

        while (true)
        {
            ParseValue(ref idx, depth + 1);
            if (idx >= _structuralCount) throw SimdJsonException.Truncated();
            int pos = _structurals[idx];
            byte sep = _input[pos];
            idx++;
            if (sep == (byte)']') break;
            if (sep != (byte)',') throw SimdJsonException.Unexpected(sep, pos);
        }

        _tape[startTape] = Encode(JsonElementType.Array, _tapeIndex);
    }

    private static ReadOnlySpan<byte> StringTerminators => "\"\\"u8;
    private static ReadOnlySpan<byte> NumberTerminators => " \t\r\n,}]"u8;

    private void ParseString(ref int idx)
    {
        int pos = _structurals[idx];
        int start = pos + 1;
        EnsureStringBuffer(_inputLength - start + 1);
        int writeStart = _stringBufferIndex;

        ReadOnlySpan<byte> input = _input.AsSpan(0, _inputLength);
        int p = start;

        while (true)
        {
            // Bulk-scan the run of plain bytes up to the next " or \.
            int rel = input[p..].IndexOfAny(StringTerminators);
            if (rel < 0) throw SimdJsonException.Truncated();

            // Bulk-copy the plain run.
            if (rel > 0)
            {
                input.Slice(p, rel).CopyTo(_stringBuffer.AsSpan(_stringBufferIndex));
                _stringBufferIndex += rel;
                p += rel;
            }

            byte c = input[p];
            if (c == (byte)'"')
            {
                int len = _stringBufferIndex - writeStart;
                long payload = ((long)len << 32) | (uint)writeStart;
                Write(JsonElementType.String, payload);
                idx++;
                return;
            }

            // c == '\\'
            if (p + 1 >= _inputLength) throw SimdJsonException.Truncated();
            byte esc = input[p + 1];
            switch (esc)
            {
                case (byte)'"':  _stringBuffer[_stringBufferIndex++] = (byte)'"';  p += 2; break;
                case (byte)'\\': _stringBuffer[_stringBufferIndex++] = (byte)'\\'; p += 2; break;
                case (byte)'/':  _stringBuffer[_stringBufferIndex++] = (byte)'/';  p += 2; break;
                case (byte)'b':  _stringBuffer[_stringBufferIndex++] = 0x08; p += 2; break;
                case (byte)'f':  _stringBuffer[_stringBufferIndex++] = 0x0C; p += 2; break;
                case (byte)'n':  _stringBuffer[_stringBufferIndex++] = 0x0A; p += 2; break;
                case (byte)'r':  _stringBuffer[_stringBufferIndex++] = 0x0D; p += 2; break;
                case (byte)'t':  _stringBuffer[_stringBufferIndex++] = 0x09; p += 2; break;
                case (byte)'u':
                    if (p + 6 > _inputLength) throw SimdJsonException.Truncated();
                    int cp = ParseHex4(input.Slice(p + 2, 4));
                    EnsureStringBuffer(4);
                    _stringBufferIndex += EncodeUtf8(cp, _stringBuffer.AsSpan(_stringBufferIndex));
                    p += 6;
                    break;
                default:
                    throw new SimdJsonException($"Invalid escape '\\{(char)esc}' at offset {p}.");
            }
        }
    }

    private void ParseLiteral(ref int idx, ReadOnlySpan<byte> expected, JsonElementType tag)
    {
        int pos = _structurals[idx];
        if (pos + expected.Length > _inputLength
            || !_input.AsSpan(pos, expected.Length).SequenceEqual(expected))
        {
            throw SimdJsonException.Unexpected(_input[pos], pos);
        }
        Write(tag, 0);
        idx++;
    }

    private void ParseNumber(ref int idx)
    {
        int pos = _structurals[idx];
        ReadOnlySpan<byte> input = _input.AsSpan(0, _inputLength);
        int rel = input[pos..].IndexOfAny(NumberTerminators);
        int end = rel < 0 ? _inputLength : pos + rel;

        ReadOnlySpan<byte> slice = input.Slice(pos, end - pos);
        bool isFloat = ContainsFloatChar(slice);

        if (!isFloat
            && Utf8Parser.TryParse(slice, out long l, out int consumed)
            && consumed == slice.Length)
        {
            EnsureTape(2);
            _tape[_tapeIndex++] = Encode(JsonElementType.Int64, 0);
            _tape[_tapeIndex++] = l;
        }
        else if (Utf8Parser.TryParse(slice, out double d, out consumed)
                 && consumed == slice.Length)
        {
            EnsureTape(2);
            _tape[_tapeIndex++] = Encode(JsonElementType.Double, 0);
            _tape[_tapeIndex++] = BitConverter.DoubleToInt64Bits(d);
        }
        else
        {
            throw new SimdJsonException(
                $"Invalid number '{Encoding.UTF8.GetString(slice)}' at offset {pos}.");
        }
        idx++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsFloatChar(ReadOnlySpan<byte> s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            byte c = s[i];
            if (c == (byte)'.' || c == (byte)'e' || c == (byte)'E') return true;
        }
        return false;
    }

    private static int ParseHex4(ReadOnlySpan<byte> hex)
    {
        int v = 0;
        for (int i = 0; i < 4; i++)
        {
            byte c = hex[i];
            int d = c switch
            {
                >= (byte)'0' and <= (byte)'9' => c - (byte)'0',
                >= (byte)'a' and <= (byte)'f' => c - (byte)'a' + 10,
                >= (byte)'A' and <= (byte)'F' => c - (byte)'A' + 10,
                _ => throw new SimdJsonException($"Invalid \\u escape: '{(char)c}'."),
            };
            v = (v << 4) | d;
        }
        return v;
    }

    private static int EncodeUtf8(int codepoint, Span<byte> dst)
    {
        if (codepoint < 0x80)
        {
            dst[0] = (byte)codepoint;
            return 1;
        }
        if (codepoint < 0x800)
        {
            dst[0] = (byte)(0xC0 | (codepoint >> 6));
            dst[1] = (byte)(0x80 | (codepoint & 0x3F));
            return 2;
        }
        if (codepoint < 0x10000)
        {
            dst[0] = (byte)(0xE0 | (codepoint >> 12));
            dst[1] = (byte)(0x80 | ((codepoint >> 6) & 0x3F));
            dst[2] = (byte)(0x80 | (codepoint & 0x3F));
            return 3;
        }
        dst[0] = (byte)(0xF0 | (codepoint >> 18));
        dst[1] = (byte)(0x80 | ((codepoint >> 12) & 0x3F));
        dst[2] = (byte)(0x80 | ((codepoint >> 6) & 0x3F));
        dst[3] = (byte)(0x80 | (codepoint & 0x3F));
        return 4;
    }
}
