using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The pre-auth allocation bound. <c>RequestLoggingMiddleware</c> is registered at
/// <c>CleansiaStartupBase.cs:166</c> — before <c>UseExceptionHandler</c>, <c>UseRouting</c>,
/// <c>UseAuthentication</c> and <c>UseRateLimiter</c> — so its request-body read is reachable
/// anonymously, on a route that does not exist, with nothing upstream able to throttle it. Reading to
/// the end cost a measured 120.9 MB for a 28.6 MiB body (423x) which <c>SafeBody</c> then discarded;
/// ten concurrent max-size requests held ~1,068 MiB on an S1 instance shared with four other APIs.
///
/// <para><b>Why the two obvious tests do not work.</b> A log-output assertion cannot fail: the bounded
/// and unbounded reads emit byte-identical lines, which is the point of the fix — such a test stays
/// green through the fix AND through its reversion. An allocation assertion cannot work either;
/// <c>GC.GetAllocatedBytesForCurrentThread()</c> is per-thread and the read's continuation hops
/// thread-pool threads, so it returns negative figures, and the process-wide counter is too noisy under
/// parallel xUnit. So the observable is <b>bytes pulled from the request stream</b>, snapshotted inside
/// the terminal delegate.</para>
///
/// <para>The equivalence half is not decoration: without it a future "optimisation" could buy the
/// allocation back by weakening redaction and still satisfy the byte bound.</para>
/// </summary>
public class RequestLogBodyReadBoundTests
{
    private const string Secret = "sk_live_this_must_never_reach_a_log";
    private const string Marker = "leak-marker-must-not-reach-a-log";
    private const string SizeSuppressed = "[suppressed: body too large to redact]";
    private const string SensitiveSuppressed = "[suppressed: sensitive endpoint]";
    private const string Redacted = "***REDACTED***";
    private const string ObservedResponseJson = """{"city":"Brno"}""";

    // ~30x the bound below, so a read that is bounded but wrongly (a whole extra buffer, a doubled cap)
    // still passes and only an unbounded one fails.
    private const int OversizeBodyChars = 2 * 1024 * 1024;

    // StreamReader's read-ahead past the last character it hands back, plus UTF-8 alignment. Measured
    // 66,560 bytes on all five hosts (65 KiB in 1 KiB reader fills) against a 65,536-byte cap, so 4 KiB
    // is slack rather than a fitted constant. The unbounded read overshoots this by ~30x, not marginally.
    private const long ReadSlackBytes = 4 * 1024;

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    public static TheoryData<Type, LogLevel> HostDisabledInformationLevels()
    {
        var data = new TheoryData<Type, LogLevel>();
        foreach (var host in RequestLoggingHarness.AllHostMiddleware)
        foreach (var level in new[] { LogLevel.Warning, LogLevel.None })
            data.Add(host, level);
        return data;
    }

    public static TheoryData<Type, LogLevel, int> HostStatusLoggerLevels()
    {
        var data = new TheoryData<Type, LogLevel, int>();
        foreach (var host in RequestLoggingHarness.AllHostMiddleware)
        foreach (var level in new[] { LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.None })
        foreach (var status in new[] { 200, 400, 500 })
            data.Add(host, level, status);
        return data;
    }

    public static TheoryData<Type, LogLevel, int> HostEnabledResponseLevels()
    {
        var data = new TheoryData<Type, LogLevel, int>();
        foreach (var host in RequestLoggingHarness.AllHostMiddleware)
        {
            data.Add(host, LogLevel.Information, StatusCodes.Status200OK);
            data.Add(host, LogLevel.Warning, StatusCodes.Status400BadRequest);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(HostDisabledInformationLevels))]
    public async Task DisabledInformation_StillBoundsAndRewindsTheRequestWithoutLogCalls(Type middlewareType, LogLevel level)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");
        var json = AsciiBodyOfChars(OversizeBodyChars);
        var body = new CountingRequestBodyStream(Encoding.UTF8.GetBytes(json));
        long pulledWhileLogging = -1;
        string? received = null;
        byte[]? forwarded = null;
        var attempts = new List<LogLevel>();

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", responseJson: "{}", method: HttpMethods.Post,
            requestBody: body, minimumLogLevel: level,
            onNextInvoked: () => pulledWhileLogging = body.BytesRead,
            downstream: async context =>
            {
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                received = await reader.ReadToEndAsync();
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("{}"));
            },
            onCompleted: context => forwarded = ((MemoryStream)context.Response.Body).ToArray(),
            onLogAttempt: attempts.Add);

        Assert.InRange(pulledWhileLogging, 1, scanLimit + ReadSlackBytes);
        Assert.Equal(json, received);
        Assert.Equal(Encoding.UTF8.GetBytes("{}"), forwarded);
        Assert.Empty(logged);
        // Original code calls disabled request/response logs after doing their formatting work.
        // This observer is independent of whether the capturing logger stores disabled messages.
        Assert.Empty(attempts);
    }

    [Theory]
    [MemberData(nameof(HostStatusLoggerLevels))]
    public async Task StatusSpecificLogging_PreservesForwardedBytesAndOnlyCallsEnabledLevels(
        Type middlewareType, LogLevel minimumLevel, int status)
    {
        const string json = """{"apiKey":"sk_live_never_log_me","city":"Brno"}""";
        var attempts = new List<LogLevel>();
        byte[]? forwarded = null;
        var actualStatus = 0;
        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", responseJson: json,
            minimumLogLevel: minimumLevel, responseStatusCode: status,
            onCompleted: context =>
            {
                forwarded = ((MemoryStream)context.Response.Body).ToArray();
                actualStatus = context.Response.StatusCode;
            },
            onLogAttempt: attempts.Add);

        var responseLevel = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
        var expected = new List<LogLevel>();
        if (minimumLevel <= LogLevel.Information) expected.Add(LogLevel.Information);
        if (minimumLevel <= responseLevel) expected.Add(responseLevel);
        Assert.Equal(expected, attempts);
        Assert.Equal(expected.Count, logged.Count);
        Assert.Equal(status, actualStatus);
        Assert.Equal(Encoding.UTF8.GetBytes(json), forwarded);
        Assert.All(logged, message => Assert.DoesNotContain("sk_live_never_log_me", message));
        if (minimumLevel <= responseLevel)
            Assert.Contains(logged, message => message.Contains("Response: " + status)
                && message.Contains("***REDACTED***") && message.Contains("Brno"));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task CredentialCrossingTheReadChunk_IsRedactedAsOneBodyBeforeTruncation(Type middlewareType)
    {
        var json = "{\"apiKey\":\"" + Secret + new string('x', 4096) + "\",\"city\":\"Brno\"}";
        const string expected = """{"apiKey":"***REDACTED***","city":"Brno"}""";
        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", responseJson: json, requestJson: json, method: HttpMethods.Post);
        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));
        Assert.EndsWith(expected, RequestLine(logged), StringComparison.Ordinal);
        Assert.EndsWith(expected, ResponseLine(logged), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task ResponseAtTheScanLimit_IsRedactedAndOneCharacterOverIsSuppressed(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");
        var at = await RequestLoggingHarness.RunAsync(middlewareType, "/api/Order/Create", AsciiBodyOfChars(scanLimit));
        var over = await RequestLoggingHarness.RunAsync(middlewareType, "/api/Order/Create", AsciiBodyOfChars(scanLimit + 1));
        Assert.Contains(Redacted, ResponseLine(at));
        Assert.DoesNotContain(SizeSuppressed, ResponseLine(at));
        Assert.DoesNotContain(Secret, ResponseLine(at));
        Assert.Contains(SizeSuppressed, ResponseLine(over));
        Assert.DoesNotContain(Secret, ResponseLine(over));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task MultiByteResponse_OverTheCharCap_IsStillSuppressed(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");
        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", MultiByteBodyOfChars(scanLimit + 1000));
        Assert.Contains(SizeSuppressed, ResponseLine(logged));
        Assert.DoesNotContain(Marker, ResponseLine(logged));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task OversizeRequestBody_PullsOnlyTheScanBoundFromTheStream(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");
        var body = new CountingRequestBodyStream(Encoding.UTF8.GetBytes(AsciiBodyOfChars(OversizeBodyChars)));
        long pulledWhileLogging = -1;

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType,
            "/api/Order/Create",
            responseJson: "{}",
            method: HttpMethods.Post,
            requestBody: body,
            onNextInvoked: () => pulledWhileLogging = body.BytesRead);

        Assert.Contains(logged, message => message.Contains(SizeSuppressed));
        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));

        // The lower bound is the non-vacuity half: 0 would mean the callback never ran or the body was
        // never read, and either would let this pass on a middleware that logs nothing.
        Assert.InRange(pulledWhileLogging, 1, scanLimit + ReadSlackBytes);
    }

    /// <summary>
    /// The bound is in CHARACTERS, and this is what says so. A body whose CHAR length is over the cap but
    /// whose first <c>cap + 1</c> BYTES decode to fewer than <c>cap</c> characters must still be
    /// suppressed. Bounding the read by bytes would hand <c>SafeBody</c> a short string, put it under the
    /// cap, and log the prefix of a body that the whole-body read suppresses — a behaviour change, and in
    /// the leaking direction.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task MultiByteRequestBody_OverTheCharCapButUnderItInBytes_IsStillSuppressed(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");
        var json = MultiByteBodyOfChars(scanLimit + 1000);
        var bytes = Encoding.UTF8.GetBytes(json);

        Assert.True(json.Length > scanLimit, "Fixture must be over the cap measured in characters.");
        Assert.True(
            Encoding.UTF8.GetCharCount(bytes.AsSpan(0, scanLimit + 1)) <= scanLimit,
            "Fixture must be UNDER the cap measured in characters-decoded-from-cap+1-bytes, or a "
            + "byte-bounded read would reach the same verdict and this test would prove nothing.");

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.Contains(logged, message => message.Contains(SizeSuppressed));
        Assert.All(logged, message => Assert.DoesNotContain(Marker, message));
    }

    /// <summary>
    /// Exactly at the cap still redacts, one character over still suppresses. The read stops at
    /// <c>cap + 1</c> characters precisely so this pair is decided the way the whole body would decide
    /// it — a read of <c>cap</c> characters would report the second body as <c>Length == cap</c> and log
    /// its redacted prefix.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task RequestBodyOfExactlyTheScanLimit_IsRedactedNotSuppressed(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");

        var logged = await RunPost(middlewareType, AsciiBodyOfChars(scanLimit));

        Assert.All(logged, message => Assert.DoesNotContain(SizeSuppressed, message));
        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));
        Assert.Contains(logged, message => message.Contains(Redacted));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task RequestBodyOneCharacterOverTheScanLimit_IsSuppressed(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");

        var logged = await RunPost(middlewareType, AsciiBodyOfChars(scanLimit + 1));

        Assert.Contains(logged, message => message.Contains(SizeSuppressed));
        Assert.All(logged, message => Assert.DoesNotContain(Secret, message));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task OverCapRequestBody_LogsTheWholeLineUnchanged(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");

        var logged = await RunPost(middlewareType, AsciiBodyOfChars(scanLimit + 1024));

        Assert.EndsWith(
            $"POST /api/Order/Create | User: Anonymous | IP: Unknown | Body: {SizeSuppressed}",
            RequestLine(logged),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task UnderCapRequestBody_LogsTheWholeRedactedLineUnchanged(Type middlewareType)
    {
        const string json = """{"apiKey":"sk_live_never_log_me","email":"cleaner@example.cz","city":"Brno"}""";
        const string expected = """{"apiKey":"***REDACTED***","email":"***REDACTED***","city":"Brno"}""";

        var logged = await RunPost(middlewareType, json);

        Assert.EndsWith(
            $"POST /api/Order/Create | User: Anonymous | IP: Unknown | Body: {expected}",
            RequestLine(logged),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostEnabledResponseLevels))]
    public async Task SensitivePathResponse_IsForwardedWithoutBeingReadForTheLog(
        Type middlewareType, LogLevel minimumLevel, int status)
    {
        var (logged, bytesRead, forwarded) = await RunObservingTheResponseRead(
            middlewareType, "/api/Order/GetPhotos", minimumLevel, status);

        Assert.Equal(0, bytesRead);
        Assert.Equal(Encoding.UTF8.GetBytes(ObservedResponseJson), forwarded);
        Assert.Contains(SensitiveSuppressed, ResponseLine(logged));
    }

    [Theory]
    [MemberData(nameof(HostEnabledResponseLevels))]
    public async Task OrdinaryPathResponse_IsReadForTheLog(Type middlewareType, LogLevel minimumLevel, int status)
    {
        var (logged, bytesRead, forwarded) = await RunObservingTheResponseRead(
            middlewareType, "/api/Order/Create", minimumLevel, status);

        Assert.Equal(Encoding.UTF8.GetByteCount(ObservedResponseJson), bytesRead);
        Assert.Equal(Encoding.UTF8.GetBytes(ObservedResponseJson), forwarded);
        Assert.EndsWith(ObservedResponseJson, ResponseLine(logged), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task SensitivePathRequest_StillPullsTheBoundedReadWhenInformationIsEnabled(Type middlewareType)
    {
        var scanLimit = RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit");
        var json = AsciiBodyOfChars(OversizeBodyChars);
        var body = new CountingRequestBodyStream(Encoding.UTF8.GetBytes(json));
        long pulledWhileLogging = -1;
        string? received = null;

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/SavePhotos", responseJson: "{}", method: HttpMethods.Post,
            requestBody: body, minimumLogLevel: LogLevel.Information,
            onNextInvoked: () => pulledWhileLogging = body.BytesRead,
            downstream: async context =>
            {
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                received = await reader.ReadToEndAsync();
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("{}"));
            });

        Assert.InRange(pulledWhileLogging, 1, scanLimit + ReadSlackBytes);
        Assert.Equal(json, received);
        Assert.EndsWith(
            $"POST /api/Order/SavePhotos | User: Anonymous | IP: Unknown | Body: {SensitiveSuppressed}",
            RequestLine(logged),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The downstream writes into the middleware's capture stream, which is what gets forwarded, and then
    /// swaps <c>Response.Body</c> for a counting stream holding the same bytes. The response log reads
    /// <c>Response.Body</c>, so the counter says whether the log read the response at all.
    /// </summary>
    private static async Task<(List<string> Logged, long BytesRead, byte[]? Forwarded)> RunObservingTheResponseRead(
        Type middlewareType, string path, LogLevel minimumLevel, int status)
    {
        var bytes = Encoding.UTF8.GetBytes(ObservedResponseJson);
        var observed = new CountingRequestBodyStream(bytes);
        byte[]? forwarded = null;

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, path, responseJson: ObservedResponseJson, minimumLogLevel: minimumLevel,
            downstream: async context =>
            {
                context.Response.StatusCode = status;
                await context.Response.Body.WriteAsync(bytes);
                context.Response.Body = observed;
            },
            onCompleted: context => forwarded = ((MemoryStream)context.Response.Body).ToArray());

        return (logged, observed.BytesRead, forwarded);
    }

    private static Task<List<string>> RunPost(Type middlewareType, string json) =>
        RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/Create", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

    /// <summary>The request line is the only one carrying <c>IP:</c>; the response line carries <c>Response:</c>.</summary>
    private static string RequestLine(List<string> logged) =>
        Assert.Single(logged, message => message.Contains(" | IP: ", StringComparison.Ordinal));

    private static string ResponseLine(List<string> logged) =>
        Assert.Single(logged, message => message.Contains(" | Response: ", StringComparison.Ordinal));

    /// <summary>
    /// Exactly <paramref name="chars"/> single-byte characters, with the secret FIRST so a scan that ran
    /// would certainly have redacted it and a log showing it certainly did not scan.
    /// </summary>
    private static string AsciiBodyOfChars(int chars)
    {
        var head = "{\"apiKey\":\"" + Secret + "\",\"filler\":\"";
        const string tail = "\"}";
        var padding = chars - head.Length - tail.Length;
        Assert.True(padding > 0);
        return head + new string('x', padding) + tail;
    }

    /// <summary>Exactly <paramref name="chars"/> characters, nearly all of them two bytes in UTF-8.</summary>
    private static string MultiByteBodyOfChars(int chars)
    {
        var head = "{\"filler\":\"" + Marker;
        const string tail = "\"}";
        var padding = chars - head.Length - tail.Length;
        Assert.True(padding > 0);
        return head + new string('é', padding) + tail;
    }
}
