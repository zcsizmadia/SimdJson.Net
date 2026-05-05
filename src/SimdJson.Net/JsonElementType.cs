namespace SimdJson.Net;

/// <summary>
/// Logical type of a <see cref="JsonElement"/>.
/// </summary>
public enum JsonElementType : byte
{
    Undefined = 0,
    Null,
    True,
    False,
    Int64,
    UInt64,
    Double,
    String,
    Array,
    Object,
}
