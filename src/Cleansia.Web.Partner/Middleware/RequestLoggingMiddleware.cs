using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace Cleansia.Web.Partner.Middleware;

public partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string Redacted = "\"***REDACTED***\"";
    private const int RequestBodyLimit = 1000;
    private const int ResponseBodyLimit = 500;

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
        var userEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "N/A";

        var rawBody = await ReadRequestBodyAsync(request);
        var safeBody = IsSensitivePath(request.Path)
            ? "[suppressed: sensitive endpoint]"
            : RedactSensitiveFields(rawBody);

        _logger.LogInformation(
            "[{RequestId}] {Method} {Path}{QueryString} | User: {UserId} ({UserEmail}) | IP: {IP} | Body: {Body}",
            requestId,
            request.Method,
            request.Path,
            request.QueryString,
            userId,
            userEmail,
            context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            string.IsNullOrEmpty(safeBody) ? "N/A" : safeBody
        );
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long durationMs)
    {
        var response = context.Response;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        var rawBody = await ReadResponseBodyAsync(response);
        var safeBody = IsSensitivePath(context.Request.Path)
            ? "[suppressed: sensitive endpoint]"
            : RedactSensitiveFields(TruncateBody(rawBody, ResponseBodyLimit));

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

        return TruncateBody(body, RequestBodyLimit);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        response.Body.Seek(0, SeekOrigin.Begin);

        return body;
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

    private static bool IsSensitivePath(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        return pathValue.Contains("/auth/") || pathValue.Contains("/login") || pathValue.Contains("password");
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

    [GeneratedRegex("\"(password|currentPassword|newPassword|confirmPassword|token|refreshToken|accessToken|clientSecret|apiKey|base64Content|fileData|fileBase64)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|null)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SensitiveFieldRegex();
}
