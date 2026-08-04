using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace Cleansia.Web.Admin.Middleware;

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
            request.EnableBuffering();
        }

        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        request.Body.Position = 0;

        return body;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        response.Body.Seek(0, SeekOrigin.Begin);

        return body;
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

    private static string RedactSensitiveFields(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        return SensitiveFieldRegex().Replace(body, m => $"\"{m.Groups[1].Value}\":{Redacted}");
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
               // block too.
               pathValue.Contains("/gdpr");
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

    [GeneratedRegex(@"([?&][^=&]*email[^=&]*=)[^&]*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailQueryParamRegex();
}
