namespace SimdJson;

/// <summary>
/// Represents a parsed JSON number with its exact numeric type and value.
/// Returned by <see cref="JsonValue.GetNumber()"/>.
/// </summary>
public readonly struct JsonNumber
{
    private readonly JsonNumberType _type;
    private readonly double         _floatingPoint;
    private readonly long           _signedInteger;
    private readonly ulong          _unsignedInteger;

    internal JsonNumber(JsonNumberType type, double f, long i, ulong u)
    {
        _type            = type;
        _floatingPoint   = f;
        _signedInteger   = i;
        _unsignedInteger = u;
    }

    /// <summary>The numeric sub-type of this number.</summary>
    public JsonNumberType NumberType => _type;

    /// <summary>
    /// Returns the value as a <see cref="double"/>.
    /// For floating-point numbers this is exact; for integers it may lose precision.
    /// </summary>
    public double AsDouble() => _type == JsonNumberType.FloatingPoint
        ? _floatingPoint
        : _type == JsonNumberType.UnsignedInteger
            ? (double)_unsignedInteger
            : (double)_signedInteger;

    /// <summary>
    /// Returns the value as a <see cref="long"/>. Only meaningful when
    /// <see cref="NumberType"/> is <see cref="JsonNumberType.SignedInteger"/>.
    /// </summary>
    public long AsInt64() => _signedInteger;

    /// <summary>
    /// Returns the value as a <see cref="ulong"/>. Only meaningful when
    /// <see cref="NumberType"/> is <see cref="JsonNumberType.UnsignedInteger"/>.
    /// </summary>
    public ulong AsUInt64() => _unsignedInteger;

    /// <inheritdoc />
    public override string ToString() => _type switch
    {
        JsonNumberType.FloatingPoint   => _floatingPoint.ToString(),
        JsonNumberType.SignedInteger   => _signedInteger.ToString(),
        JsonNumberType.UnsignedInteger => _unsignedInteger.ToString(),
        _                              => "big_integer",
    };
}
