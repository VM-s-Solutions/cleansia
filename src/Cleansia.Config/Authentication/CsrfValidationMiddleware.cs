using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cleansia.Config.Authentication;

/// <summary>
/// Validates the <c>X-CSRF-Token</c> header on state-changing requests. No-ops on safe methods, on
/// opt-out paths (Stripe webhook, login, register, refresh — which either carry their own auth or have
/// no session yet), and on unauthenticated requests; otherwise compares against the token derived from
/// the session JWT and 403s on mismatch.
///
/// <para><b>Registration is host-specific because the opt-out paths differ per host</b>, and the
/// middleware MUST sit after auth so the user is resolved. → /architecture/security-rules</para>
/// </summary>
public class CsrfValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CsrfTokenService _csrfTokenService;
    private readonly CsrfOptions _options;
    private readonly ILogger<CsrfValidationMiddleware> _logger;

    public CsrfValidationMiddleware(
        RequestDelegate next,
        CsrfTokenService csrfTokenService,
        CsrfOptions options,
        ILogger<CsrfValidationMiddleware> logger)
    {
        _next = next;
        _csrfTokenService = csrfTokenService;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !RequiresValidation(context))
        {
            await _next(context);
            return;
        }

        var sessionKey = CsrfTokenService.GetSessionKey(context.User);
        if (string.IsNullOrEmpty(sessionKey))
        {
            // No session to derive from. Let auth middleware deal with it.
            await _next(context);
            return;
        }

        var headerValue = context.Request.Headers["X-CSRF-Token"].ToString();
        if (string.IsNullOrEmpty(headerValue))
        {
            _logger.LogWarning(
                "CSRF rejection: missing X-CSRF-Token header on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await Forbid(context, "csrf.header_missing");
            return;
        }

        var expected = _csrfTokenService.Derive(sessionKey);
        if (!FixedTimeEquals(headerValue, expected))
        {
            _logger.LogWarning(
                "CSRF rejection: mismatched X-CSRF-Token on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await Forbid(context, "csrf.header_mismatch");
            return;
        }

        await _next(context);
    }

    private bool RequiresValidation(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method))
        {
            return false;
        }

        if (context.User.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var optOut in _options.OptOutPaths)
        {
            if (path.StartsWith(optOut, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static Task Forbid(HttpContext context, string reasonCode)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers["X-Csrf-Failure"] = reasonCode;
        return context.Response.WriteAsync($"{{\"error\":\"{reasonCode}\"}}");
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}

/// <summary>
/// Configuration for <see cref="CsrfValidationMiddleware"/>. Built from
/// `appsettings.json` (`Csrf:Enabled`, `Csrf:Secret`) plus host-specific
/// opt-out paths registered at startup.
/// </summary>
public class CsrfOptions
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Request-path prefixes that bypass CSRF validation. Each host adds
    /// its own list at startup (webhook endpoints, auth endpoints, etc.)
    /// — see `CsrfMiddlewareExtensions.UseCsrfValidation`.
    /// </summary>
    public IReadOnlyList<string> OptOutPaths { get; set; } = Array.Empty<string>();
}
