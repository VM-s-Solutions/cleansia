using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The cost bound on redaction. Redacting BEFORE truncating (this ticket's AC9 fix) is required for
/// correctness — the regex needs a value's closing quote — but it means the scan covers the WHOLE body
/// instead of the first KB. Measured: 1 MB → 187 ms, 10 MB → 2351 ms, 25 MB → 5716 ms, synchronously on
/// the request thread before <c>_next</c>, on every request. With no server-side image cap beyond
/// Kestrel's 30 MB default, an authenticated caller could spend seconds of CPU per call.
///
/// So bodies past <c>RedactionScanLimit</c> are suppressed WHOLESALE rather than scanned. Suppressed,
/// not truncated — truncating an unscanned body is exactly the prefix leak AC9 removed.
///
/// These pin both sides of the boundary: under the cap still redacts (the bound did not silently
/// disable redaction), over the cap suppresses (the bound is actually enforced).
/// </summary>
public class RequestLogRedactionScanLimitTests
{
    private const string Secret = "sk_live_this_must_never_reach_a_log";

    // FIXED sizes, deliberately NOT derived from RedactionScanLimit. Deriving them made the mutation
    // "set the limit to 0" trip the fixtures' own padding guard instead of the assertions, so the
    // bound-silently-disabled scenario could be argued but never DEMONSTRATED. With fixed sizes,
    // limit=0 leaves the over-cap test correctly green and turns the under-cap one red, which is
    // exactly the failure a disabled bound produces.
    private const int UnderCapSize = 2 * 1024;
    private const int OverCapSize = 128 * 1024;
    private const string SizeSuppressed = "[suppressed: body too large to redact]";
    private const string Redacted = "***REDACTED***";

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task RequestBody_JustUnderTheScanLimit_IsStillRedacted(Type middlewareType)
    {
        var logged = await RunRequest(middlewareType, BodyOfLength(UnderCapSize));

        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));
        Assert.All(logged, message => Assert.DoesNotContain(SizeSuppressed, message));
        Assert.Contains(logged, message => message.Contains(Redacted));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task RequestBody_OverTheScanLimit_IsSuppressedWholesale(Type middlewareType)
    {
        var logged = await RunRequest(middlewareType, BodyOfLength(OverCapSize));

        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));
        Assert.Contains(logged, message => message.Contains(SizeSuppressed));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task ResponseBody_OverTheScanLimit_IsSuppressedWholesale(Type middlewareType)
    {
        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/User/GetCurrent", responseJson: BodyOfLength(OverCapSize));

        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));
        Assert.Contains(logged, message => message.Contains(SizeSuppressed));
    }

    /// <summary>
    /// The fixtures only mean anything while they bracket the real cap. This says so out loud, so a
    /// future change to RedactionScanLimit fails HERE with a clear reason rather than quietly turning
    /// one of the boundary tests into a no-op.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public void TheFixturesStillBracketTheConfiguredCap(Type middlewareType)
    {
        var cap = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");

        Assert.InRange(cap, UnderCapSize + 1, OverCapSize - 1);
    }

    private static Task<List<string>> RunRequest(Type middlewareType, string json) =>
        RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

    /// <summary>
    /// A body of exactly <paramref name="length"/> characters whose FIRST field is the secret, so a
    /// scan that runs would certainly redact it and a log that shows it certainly did not scan.
    /// </summary>
    private static string BodyOfLength(int length)
    {
        var head = "{\"apiKey\":\"" + Secret + "\",\"filler\":\"";
        const string tail = "\"}";
        var padding = length - head.Length - tail.Length;
        Assert.True(padding > 0);
        return head + new string('x', padding) + tail;
    }
}
