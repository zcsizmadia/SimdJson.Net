using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SimdJson.Net.Internal;

/// <summary>
/// Stage 1 of the simdjson pipeline. Scans raw UTF-8 input and produces a
/// dense list of byte offsets pointing at every "structural" character
/// (<c>{ } [ ] : ,</c>), every unescaped string boundary (<c>"</c>), and the
/// first byte of every JSON atom (numbers, <c>true</c>, <c>false</c>, <c>null</c>).
/// </summary>
internal static class StructuralIndexer
{
    public const int BlockSize = 64;

    public static int EstimateMaxIndices(int byteLength) => byteLength + 64;

    public static int Index(ReadOnlySpan<byte> input, Span<int> indices)
    {
        if (indices.Length < EstimateMaxIndices(input.Length))
            throw new ArgumentException("Index buffer too small.", nameof(indices));

        int count = 0;
        bool prevInString = false;
        bool prevEscape = false;
        bool prevScalar = false;

        ref byte inputRef = ref MemoryMarshal.GetReference(input);
        int len = input.Length;
        int i = 0;

        for (; i + BlockSize <= len; i += BlockSize)
        {
            ProcessBlock(ref Unsafe.Add(ref inputRef, i), i, BlockSize,
                indices, ref count, ref prevInString, ref prevEscape, ref prevScalar);
        }

        if (i < len)
        {
            Span<byte> tail = stackalloc byte[BlockSize];
            tail.Fill((byte)' ');
            input[i..].CopyTo(tail);
            ProcessBlock(ref MemoryMarshal.GetReference(tail), i, len - i,
                indices, ref count, ref prevInString, ref prevEscape, ref prevScalar);
        }

        if (prevInString)
            throw new SimdJsonException("Unterminated string literal.");

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessBlock(
        ref byte block, int blockOffset, int validBytes,
        Span<int> indices, ref int count,
        ref bool prevInString, ref bool prevEscape, ref bool prevScalar)
    {
        ulong quotes      = MatchByte(ref block, (byte)'"');
        ulong backslashes = MatchByte(ref block, (byte)'\\');
        ulong structural  = ClassifyStructural(ref block);
        ulong whitespace  = ClassifyWhitespace(ref block);

        if (validBytes < BlockSize)
        {
            ulong validMask = validBytes == 0 ? 0UL : (~0UL >> (BlockSize - validBytes));
            quotes      &= validMask;
            backslashes &= validMask;
            structural  &= validMask;
        }

        ulong escaped    = ComputeEscaped(backslashes, ref prevEscape);
        ulong realQuotes = quotes & ~escaped;

        ulong inString = PrefixXor(realQuotes);
        if (prevInString) inString = ~inString;
        prevInString = (inString & (1UL << 63)) != 0;

        // Emit only opening quotes: the tape builder walks the body to
        // find the matching close. inString is set at every byte from the
        // open quote up to (but not including) the close.
        ulong openingQuotes = realQuotes & inString;
        ulong structuralOutside = (structural & ~inString) | openingQuotes;

        ulong nonWsOutside    = ~whitespace & ~inString & ~structural & ~realQuotes;
        ulong followsBoundary = ((whitespace | structural | realQuotes) << 1)
                                | (prevScalar ? 0UL : 1UL);
        ulong pseudo          = nonWsOutside & followsBoundary;
        structuralOutside    |= pseudo;

        prevScalar = (nonWsOutside & (1UL << 63)) != 0;

        ulong bits = structuralOutside;
        while (bits != 0)
        {
            int tz = BitUtils.TrailingZeroCount(bits);
            indices[count++] = blockOffset + tz;
            bits = BitUtils.ClearLowestSetBit(bits);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MatchByte(ref byte block, byte target)
    {
        Vector256<byte> v0 = Vector256.LoadUnsafe(ref block);
        Vector256<byte> v1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref block, 32));
        Vector256<byte> t  = Vector256.Create(target);
        uint m0 = Vector256.Equals(v0, t).ExtractMostSignificantBits();
        uint m1 = Vector256.Equals(v1, t).ExtractMostSignificantBits();
        return ((ulong)m1 << 32) | m0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ClassifyStructural(ref byte block)
    {
        Vector256<byte> v0 = Vector256.LoadUnsafe(ref block);
        Vector256<byte> v1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref block, 32));
        return ((ulong)MatchAnyStructural(v1) << 32) | MatchAnyStructural(v0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MatchAnyStructural(Vector256<byte> v)
    {
        Vector256<byte> hits =
              Vector256.Equals(v, Vector256.Create((byte)'{'))
            | Vector256.Equals(v, Vector256.Create((byte)'}'))
            | Vector256.Equals(v, Vector256.Create((byte)'['))
            | Vector256.Equals(v, Vector256.Create((byte)']'))
            | Vector256.Equals(v, Vector256.Create((byte)':'))
            | Vector256.Equals(v, Vector256.Create((byte)','));
        return hits.ExtractMostSignificantBits();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ClassifyWhitespace(ref byte block)
    {
        Vector256<byte> v0 = Vector256.LoadUnsafe(ref block);
        Vector256<byte> v1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref block, 32));
        return ((ulong)MatchWs(v1) << 32) | MatchWs(v0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MatchWs(Vector256<byte> v)
    {
        Vector256<byte> hits =
              Vector256.Equals(v, Vector256.Create((byte)' '))
            | Vector256.Equals(v, Vector256.Create((byte)'\t'))
            | Vector256.Equals(v, Vector256.Create((byte)'\n'))
            | Vector256.Equals(v, Vector256.Create((byte)'\r'));
        return hits.ExtractMostSignificantBits();
    }

    // Sets bit i if position i is preceded by an odd-length run of '\'.
    // Scalar bit-walk over the (almost always sparse) backslash bitmap.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeEscaped(ulong backslash, ref bool prevOverflow)
    {
        ulong escaped = 0;
        if (prevOverflow)
        {
            escaped |= 1UL;
            backslash &= ~1UL;
            prevOverflow = false;
        }
        int i = 0;
        while (i < 64)
        {
            ulong rest = i == 64 ? 0UL : backslash >> i;
            if (rest == 0) break;
            int relStart = BitUtils.TrailingZeroCount(rest);
            int start = i + relStart;
            int j = start;
            while (j < 64 && ((backslash >> j) & 1UL) != 0UL) j++;
            int len = j - start;
            if ((len & 1) == 1)
            {
                if (j < 64) escaped |= 1UL << j;
                else prevOverflow = true;
            }
            i = j + 1;
        }
        return escaped;
    }

    // Lowered to a single PCLMULQDQ on x86; portable 6-step fallback otherwise.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong PrefixXor(ulong bits)
    {
        if (System.Runtime.Intrinsics.X86.Pclmulqdq.IsSupported)
        {
            var v = Vector128.CreateScalar(bits).AsUInt64();
            var ones = Vector128.Create(~0UL, 0UL);
            var prod = System.Runtime.Intrinsics.X86.Pclmulqdq.CarrylessMultiply(v, ones, 0x00);
            return prod.GetElement(0);
        }
        bits ^= bits << 1;
        bits ^= bits << 2;
        bits ^= bits << 4;
        bits ^= bits << 8;
        bits ^= bits << 16;
        bits ^= bits << 32;
        return bits;
    }

    [Conditional("DEBUG")]
    internal static void AssertBlockAlignment() => Debug.Assert(BlockSize == 64);
}
