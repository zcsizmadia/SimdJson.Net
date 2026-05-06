namespace SimdJson;

/// <summary>
/// The JSON type of a value, matching the native SimdJsonType enum.
/// </summary>
public enum JsonValueKind : int
{
    Unknown = 0,
    Array   = 1,
    Object  = 2,
    Number  = 3,
    String  = 4,
    Boolean = 5,
    Null    = 6
}
