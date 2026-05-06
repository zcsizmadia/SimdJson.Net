namespace SimdJson;

/// <summary>Exception thrown when the native simdjson bridge returns an error.</summary>
public sealed class SimdJsonException : Exception
{
    /// <summary>The raw native error code returned by the bridge.</summary>
    public int ErrorCode { get; }

    public SimdJsonException(int errorCode)
        : base(GetMessage(errorCode))
    {
        ErrorCode = errorCode;
    }

    public SimdJsonException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    internal static void ThrowIfError(int code)
    {
        if (code != 0)
        {
            Throw(code);
        }
    }

    internal static void Throw(int code) =>
        throw new SimdJsonException(code, GetMessage(code));

    private static string GetMessage(int code) => code switch
    {
        -1  => "Parser capacity exceeded.",
        -2  => "Incorrect JSON value type.",
        -3  => "No such field.",
        -4  => "Index out of bounds.",
        -5  => "Null pointer passed to native bridge.",
        -6  => "JSON parse error.",
        -7  => "Iteration error.",
        -8  => "Invalid JSON pointer.",
        -9  => "Scalar document cannot be used as a value.",
        -99 => "Unknown native error.",
        _   => $"Native error {code}."
    };
}
