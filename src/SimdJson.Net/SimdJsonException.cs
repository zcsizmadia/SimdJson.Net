namespace SimdJson.Net;

/// <summary>Thrown when the input is not well-formed JSON.</summary>
public sealed class SimdJsonException : Exception
{
    public SimdJsonException(string message) : base(message) { }
    public SimdJsonException(string message, Exception inner) : base(message, inner) { }

    internal static SimdJsonException Unexpected(byte b, int offset)
        => new($"Unexpected byte 0x{b:X2} ('{(char)b}') at offset {offset}.");

    internal static SimdJsonException Truncated()
        => new("JSON input ended unexpectedly.");
}
