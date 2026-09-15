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

    // ── Classification used by the non-throwing TryXxx helpers ────────────────
    //
    // A TryXxx method should report "the thing you asked for is not here" and nothing
    // else. Absorbing every error would also hide caller mistakes such as out-of-order
    // iteration, and document-level problems such as malformed JSON, behind a bare
    // false. Those propagate instead.

    /// <summary>
    /// The lookup found nothing: no such field, index past the end, or a pointer that
    /// does not resolve.
    /// </summary>
    internal static bool IsLookupMiss(int code) => code is -3 or -4 or -8;

    /// <summary>
    /// The value is not of the requested type, or does not fit it. Used by the typed
    /// getters, where answering "no" is the entire purpose of the call.
    /// </summary>
    internal static bool IsTypeMismatch(int code) => code is -2 or -9 or -10;

    /// <summary>
    /// As <see cref="IsTypeMismatch"/>, plus a number that does not parse. Used by the
    /// number-in-string getters, where the string's contents not being numeric is an
    /// expected answer rather than a broken document.
    /// </summary>
    internal static bool IsValueMismatch(int code) => IsTypeMismatch(code) || code is -6;

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
        -14 => "The buffer does not have enough padding after the JSON.",
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
