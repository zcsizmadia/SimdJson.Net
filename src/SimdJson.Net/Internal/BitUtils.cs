using System.Runtime.CompilerServices;

namespace SimdJson.Net.Internal;

/// <summary>
/// Bit utilities used by the parser. Wraps modern .NET intrinsics
/// (<see cref="System.Numerics.BitOperations"/>) so the parser remains
/// portable across architectures that lack BMI/POPCNT.
/// </summary>
internal static class BitUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(ulong value) => System.Numerics.BitOperations.TrailingZeroCount(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong value) => System.Numerics.BitOperations.PopCount(value);

    /// <summary>Clears the lowest set bit (BMI1 BLSR equivalent).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ClearLowestSetBit(ulong value) => value & (value - 1);
}
