namespace SimdJson;

/// <summary>Discriminates the sub-type of a JSON number value.</summary>
public enum JsonNumberType
{
    /// <summary>A floating-point number (e.g. <c>3.14</c>, <c>1e10</c>).</summary>
    FloatingPoint = 0,

    /// <summary>A signed 64-bit integer in the range [−9223372036854775808, 9223372036854775807].</summary>
    SignedInteger = 1,

    /// <summary>
    /// An unsigned 64-bit integer in the range [9223372036854775808, 18446744073709551615]
    /// (i.e. values too large to fit in a signed <see cref="long"/>).
    /// </summary>
    UnsignedInteger = 2,

    /// <summary>
    /// An integer outside the 64-bit range.
    /// Use <see cref="JsonValue.GetRawJsonToken"/> to read it as text.
    /// </summary>
    BigInteger = 3
}
