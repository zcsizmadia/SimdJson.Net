using SimdJson.Internal;

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
        -10 => "Number out of range.",
        -11 => "Native memory allocation failed.",
        -12 => "Maximum JSON nesting depth exceeded.",
        -13 => "Unexpected trailing content after the JSON value.",
        -99 => "Unknown native error.",
        _   => UpstreamMessage(code) ?? $"Native error {code}."
    };

    /// <summary>
    /// Returns simdjson's own message for error codes that carry one, or <see langword="null"/>.
    /// Codes at or below -1000 encode the original simdjson error rather than collapsing to -99.
    /// </summary>
    private static unsafe string? UpstreamMessage(int code)
    {
        if (code > UpstreamErrorBase)
        {
            return null;
        }

        try
        {
            if (NativeMethods.ErrorMessage(code, out byte* ptr, out nuint len) != 0 || ptr is null)
            {
                return null;
            }

            return $"{System.Text.Encoding.UTF8.GetString(ptr, (int)len)} (simdjson error {UpstreamErrorBase - code}).";
        }
        catch
        {
            // Building an exception message must never itself throw, even if the native
            // library failed to load.
            return null;
        }
    }

    /// <summary>Bridge codes at or below this value encode a simdjson error code.</summary>
    private const int UpstreamErrorBase = -1000;
}
