using System.Diagnostics;
using System.Security.Claims;
using System.Text;

namespace Cleansia.Web.Mobile.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health checks and static files
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();

        // Log request
        await LogRequestAsync(context, requestId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            stopwatch.Stop();

            // Log response
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);

            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
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

        var requestBody = await ReadRequestBodyAsync(request);

        _logger.LogInformation(
            "[{RequestId}] {Method} {Path}{QueryString} | User: {UserId} ({UserEmail}) | IP: {IP} | Body: {Body}",
            requestId,
            request.Method,
            request.Path,
            request.QueryString,
            userId,
            userEmail,
            context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            string.IsNullOrEmpty(requestBody) ? "N/A" : requestBody
        );
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long durationMs)
    {
        var response = context.Response;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        var responseBody = await ReadResponseBodyAsync(response);

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error :
                      response.StatusCode >= 400 ? LogLevel.Warning :
                      LogLevel.Information;

        _logger.Log(
            logLevel,
            "[{RequestId}] Response: {StatusCode} | Duration: {Duration}ms | User: {UserId} | Body: {Body}",
            requestId,
            response.StatusCode,
            durationMs,
            userId,
            string.IsNullOrEmpty(responseBody) ? "N/A" : TruncateBody(responseBody, 500)
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

        return TruncateBody(body, 1000);
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

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;

        return pathValue.Contains("/health") ||
               pathValue.Contains("/swagger") ||
               pathValue.Contains(".js") ||
               pathValue.Contains(".css") ||
               pathValue.Contains(".map") ||
               pathValue.Contains("/hangfire");
    }
}
