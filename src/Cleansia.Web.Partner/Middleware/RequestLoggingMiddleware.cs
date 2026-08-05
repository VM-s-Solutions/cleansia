using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Cleansia.Config.Abstractions;

namespace Cleansia.Web.Partner.Middleware;

public partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string Redacted = "\"***REDACTED***\"";
    private const int RequestBodyLimit = 1000;
    private const int ResponseBodyLimit = 500;

    // Redacting scans the WHOLE body (see SafeBody), so it must be bounded: without a cap an
    // authenticated caller can spend seconds of request-thread CPU per call on a multi-MB upload.
    private const int RedactionScanLimit = 64 * 1024;

    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();

        await LogRequestAsync(context, requestId);

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            stopwatch.Stop();
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnect — Information-level only, no Sentry noise.
            // See Customer middleware for the full rationale.
            stopwatch.Stop();
            _logger.LogInformation(
                "[{RequestId}] Request cancelled by client | Duration: {Duration}ms | Path: {Path}",
                requestId,
                stopwatch.ElapsedMilliseconds,
                context.Request.Path);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError(context, requestId, stopwatch.ElapsedMilliseconds, ex);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        var rawBody = await ReadRequestBodyAsync(request);
        var safeBody = SafeBody(request.Path, rawBody, RequestBodyLimit);

        _logger.LogInformation(
            "[{RequestId}] {Method} {Path}{QueryString} | User: {UserId} | IP: {IP} | Body: {Body}",
            requestId,
            request.Method,
            request.Path,
            RedactQueryString(request.QueryString),
            userId,
            context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            string.IsNullOrEmpty(safeBody) ? "N/A" : safeBody
        );
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long durationMs)
    {
        var response = context.Response;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        var rawBody = await ReadResponseBodyAsync(response);
        var safeBody = SafeBody(context.Request.Path, rawBody, ResponseBodyLimit);

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error :
                      response.StatusCode >= 400 ? LogLevel.Warning :
                      LogLevel.Information;

        _logger.Log(
            logLevel,
            "[{RequestId}] {Method} {Path} | Response: {StatusCode} | Duration: {Duration}ms | User: {UserId} | Body: {Body}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            response.StatusCode,
            durationMs,
            userId,
            string.IsNullOrEmpty(safeBody) ? "N/A" : safeBody
        );
    }

    private void LogError(HttpContext context, string requestId, long durationMs, Exception ex)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        _logger.LogError(
            ex,
            "[{RequestId}] Exception occurred | Duration: {Duration}ms | User: {UserId} | Path: {Path}",
            requestId,
            durationMs,
            userId,
            context.Request.Path
        );
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.Body.CanSeek)
        {
            request.EnableBuffering(
                CleansiaStartupBase.RequestBufferThresholdBytes,
                CleansiaStartupBase.RequestBufferLimitBytes);
        }

        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await ReadBoundedAsync(reader);

        request.Body.Position = 0;

        return body;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await ReadBoundedAsync(reader);

        response.Body.Seek(0, SeekOrigin.Begin);

        return body;
    }

    /// <summary>
    /// Reads at most <see cref="RedactionScanLimit"/> + 1 characters, never the whole body. This
    /// middleware runs before authentication and before the rate limiter, so reading to the end made an
    /// anonymous request of Kestrel's maximum size a ~121 MB allocation that <see cref="SafeBody"/> then
    /// discarded — 423x the body, with nothing upstream to throttle it.
    ///
    /// The bound is in CHARACTERS because SafeBody's verdict is <c>string.Length</c>: one character past
    /// the cap decides it exactly as the whole body would, so every log line is unchanged. A byte bound
    /// would put a multi-byte body under the cap and log what must be suppressed.
    /// </summary>
    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var buffer = new char[RedactionScanLimit + 1];
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(total));
            if (read == 0) break;
            total += read;
        }

        return new string(buffer, 0, total);
    }

    /// <summary>
    /// Redact BEFORE truncating. The regex matches a complete quoted value, so truncating first leaves
    /// the raw visible prefix of any secret whose closing quote falls past the cut.
    ///
    /// Redacting first costs a scan of the whole body, so anything past <see cref="RedactionScanLimit"/>
    /// is suppressed outright rather than scanned or truncated — bounding the per-request cost without
    /// reopening the prefix leak that truncating first would.
    /// </summary>
    private static string SafeBody(PathString path, string rawBody, int logLimit)
    {
        if (IsSensitivePath(path))
        {
            return "[suppressed: sensitive endpoint]";
        }

        if (rawBody.Length > RedactionScanLimit)
        {
            return "[suppressed: body too large to redact]";
        }

        return TruncateBody(RedactSensitiveFields(rawBody), logLimit);
    }

    private static string TruncateBody(string body, int maxLength)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= maxLength)
        {
            return body;
        }

        return body.Substring(0, maxLength) + "... (truncated)";
    }

    /// <summary>
    /// Two passes because the two families are not the same defect and must not be reasoned about as
    /// one. A credential/payload value is unbounded — a base64 image, a signed URL, a JWT — so collapsing
    /// it to the sentinel FREES hundreds of bytes of window and drags whatever follows it into the log
    /// (the class <c>RedactionUnmaskedFreeTextGuardTests</c> exists for). A contact-identity value is
    /// bounded at tens of bytes, so its collapse shifts the window by tens of bytes, and for the short
    /// ones it LENGTHENS the body. Keeping them in one alternation would have made every string member of
    /// every DTO read as "unmasked", which is how a guard stops being informative.
    /// </summary>
    private static string RedactSensitiveFields(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var redacted = SensitiveFieldRegex().Replace(body, m => $"\"{m.Groups[1].Value}\":{Redacted}");
        return ContactIdentityFieldRegex().Replace(redacted, m => $"\"{m.Groups[1].Value}\":{Redacted}");
    }

    private static string RedactQueryString(QueryString queryString)
    {
        if (!queryString.HasValue) return string.Empty;
        return EmailQueryParamRegex().Replace(queryString.Value!, "$1***REDACTED***");
    }

    private static bool IsSensitivePath(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        return pathValue.Contains("/auth/") ||
               // "/auth/" does NOT match "/api/AdminAuth/..." — no slash precedes "auth" there — so the
               // admin refresh/logout responses fell outside it. They return a JwtTokenResponse whose
               // leading JWT is redacted, freeing ~785 bytes and pulling the admin's Email into view.
               pathValue.Contains("/adminauth/") ||
               pathValue.Contains("/login") ||
               pathValue.Contains("password") ||
               pathValue.Contains("/order/lookup") ||
               // Operator free text riding beside a base64 payload — an identity-document description
               // and a cleaner's note about a customer's household. No field-name denylist can reach
               // free text, so the whole body is suppressed.
               pathValue.Contains("/savemydocuments") ||
               pathValue.Contains("/savephotos") ||
               pathValue.Contains("/uploadphoto") ||
               // Reading photos back leaks the same way: redacting the SAS frees ~170 bytes of window
               // and pulls the trailing per-photo note in. Two route shapes — the four hosts'
               // /api/Order/GetPhotos and the admin /api/AdminOrder/photos/{orderId}.
               pathValue.Contains("/getphotos") ||
               pathValue.Contains("/photos/") ||
               // A cleaner's payout destination: account number, derived IBAN, BIC, and the beneficiary
               // name as their BANK knows it. None of those field names is in the redaction token list,
               // and a holder name is free text no denylist can reach — so both the write and the two
               // reads are suppressed wholesale rather than field-redacted (ADR-0034 D6/S6).
               pathValue.Contains("/updatebankdetails") ||
               pathValue.Contains("/getmypayoutdetails") ||
               pathValue.Contains("payout-details") ||
               // The subject-access export is the whole person in one body, and it now carries the payout
               // block too. Matched on the TRAILING slash, not a leading one: "/gdpr" does not match
               // "/api/v1/AdminGdpr/export/{userId}" — the same shape that let "/auth/" miss
               // "/api/AdminAuth/...". The admin route dumps another user's whole export, payout block
               // included, so it is the one that most needed covering.
               pathValue.Contains("gdpr/") ||
               // Reading documents back: an operator's review notes and the cleaner's own description
               // ride behind the SAS, which redaction collapses. The write side (/savemydocuments) was
               // already suppressed; the read was not.
               pathValue.Contains("/getmydocuments") ||
               // Cleansia's OWN account number, IBAN and BIC. It is printed on every customer receipt so
               // it is not confidential, and it is suppressed anyway: the alternative is an entry on the
               // payout guard's exception list reading "this bank account is fine to log", which is the
               // sentence that gets copied onto the next one.
               pathValue.Contains("/admincompany/");
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;

        return pathValue.Contains("/health") ||
               pathValue.Contains("/swagger") ||
               pathValue.Contains(".js") ||
               pathValue.Contains(".css") ||
               pathValue.Contains(".map") ||
               pathValue.Contains("/hangfire") ||
               pathValue.Contains("/payment/webhook");
    }

    [GeneratedRegex("\"(password|currentPassword|newPassword|confirmPassword|token|refreshToken|accessToken|clientSecret|setupIntentClientSecret|apiKey|base64Content|fileData|fileBase64|blobUrl|ephemeralKey)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|null)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SensitiveFieldRegex();

    /// <summary>
    /// Contact identity, matched by SHAPE rather than enumerated. Enumerating it was the defect: the leak
    /// was never one endpoint, it was <see cref="SafeBody"/> — generic over every route — so a per-route
    /// or per-exact-name entry fixes the instance and leaves the class open. Measured when this was
    /// written: 152 members on 80+ routes carry one of these names, and a shape covers the next one
    /// without anyone remembering.
    ///
    /// <para>The alternation is quote-anchored on both sides, so a name must match WHOLE. That is what
    /// keeps <c>emailTemplateId</c> (an id) out while catching <c>customerEmail</c>, and it is why
    /// <c>*email</c> takes no suffix while <c>*phone*</c> does. The value group matches a quoted string or
    /// <c>null</c> and nothing else, so a boolean neighbour like <c>isEmailConfirmed</c> is structurally
    /// unreachable.</para>
    ///
    /// <para>What makes a denylist fail closed is not the list — it is
    /// <c>RequestLogPiiSurfaceGuardTests</c>, which walks every wire DTO on the five hosts and reddens CI
    /// when a PII-shaped member is neither matched here nor on a suppressed route nor excepted in
    /// writing. It reads THIS regex, so the two cannot drift.</para>
    /// </summary>
    [GeneratedRegex("\"([A-Za-z]*email|[A-Za-z]*phone[A-Za-z]*|[A-Za-z]*firstName|[A-Za-z]*lastName|fullName|birthDate)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|null)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ContactIdentityFieldRegex();

    [GeneratedRegex(@"([?&][^=&]*email[^=&]*=)[^&]*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailQueryParamRegex();
}
