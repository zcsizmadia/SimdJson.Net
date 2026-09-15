namespace SimdJson.Tests;

// ─── Active SIMD implementation ───────────────────────────────────────────────

public class ActiveImplementationTests
{
    private static readonly string[] KnownKernels =
    [
        "haswell", "icelake", "westmere", "arm64", "ppc64", "lsx", "lasx", "rvv", "fallback"
    ];

    [Test]
    public async Task ActiveImplementation_IsNotEmpty()
    {
        await Assert.That(SimdJsonParser.ActiveImplementation).IsNotEmpty();
    }

    [Test]
    public async Task ActiveImplementation_IsAKnownKernelName()
    {
        // Guards against returning uninitialised buffer contents or a truncated copy.
        await Assert.That(KnownKernels).Contains(SimdJsonParser.ActiveImplementation);
    }

    [Test]
    public async Task ActiveImplementation_IsStableAcrossCalls()
    {
        var first = SimdJsonParser.ActiveImplementation;
        var second = SimdJsonParser.ActiveImplementation;
        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task ActiveImplementation_HasNoTrailingNullBytes()
    {
        var name = SimdJsonParser.ActiveImplementation;
        await Assert.That(name).IsEqualTo(name.TrimEnd('\0'));
    }
}

// ─── simdjson error message passthrough ───────────────────────────────────────

public class UpstreamErrorMessageTests
{
    [Test]
    public async Task MappedCodes_KeepTheirOwnMessages()
    {
        // Codes with a dedicated bridge value must not be rerouted through simdjson's table.
        await Assert.That(new SimdJsonException(-3).Message).IsEqualTo("No such field.");
        await Assert.That(new SimdJsonException(-13).Message)
            .IsEqualTo("Unexpected trailing content after the JSON value.");
    }

    [Test]
    public async Task UnmappedCode_CarriesUpstreamText()
    {
        // -1000 - 24 encodes simdjson's UNESCAPED_CHARS-adjacent range; any encoded code
        // must produce a message that is neither the generic fallback nor empty.
        var message = new SimdJsonException(-1024).Message;
        await Assert.That(message).IsNotEmpty();
        await Assert.That(message).DoesNotContain("Native error");
        await Assert.That(message).Contains("simdjson error 24");
    }

    [Test]
    public async Task EncodedCode_IsReportedOnErrorCodeProperty()
    {
        var ex = new SimdJsonException(-1024);
        await Assert.That(ex.ErrorCode).IsEqualTo(-1024);
    }

    [Test]
    [Arguments(-100)]
    [Arguments(-42)]
    [Arguments(-999)]
    public async Task CodesAboveTheEncodingBase_UseTheGenericFallback(int code)
    {
        await Assert.That(new SimdJsonException(code).Message).IsEqualTo($"Native error {code}.");
    }

    [Test]
    public async Task OutOfRangeEncodedCode_FallsBackInsteadOfReadingGarbage()
    {
        // Far beyond NUM_ERROR_CODES; the bridge must reject it rather than index its table.
        var message = new SimdJsonException(-99999).Message;
        await Assert.That(message).IsEqualTo("Native error -99999.");
    }

    [Test]
    public async Task UnknownCode_StillHasTheLegacyMessage()
    {
        await Assert.That(new SimdJsonException(-99).Message).IsEqualTo("Unknown native error.");
    }
}
