using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Authentication;

public static class CsrfMiddlewareExtensions
{
    /// <summary>
    /// Register the <see cref="CsrfTokenService"/> singleton and
    /// <see cref="CsrfOptions"/> bound to the <c>Csrf</c> configuration
    /// section. The middleware itself is registered via
    /// <see cref="UseCsrfValidation"/> in the pipeline. Caller passes the
    /// host-specific opt-out paths (e.g. webhook routes) so the middleware
    /// knows which endpoints to skip.
    /// </summary>
    public static IServiceCollection AddCsrfProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<string> optOutPaths)
    {
        var section = configuration.GetSection("Csrf");
        var enabled = section.GetValue<bool>("Enabled");
        var secret = section.GetValue<string>("Secret") ?? string.Empty;

        // Even when disabled we register the service so endpoints that return CSRF tokens in their
        // response body (auth controllers) can derive them — the value is meaningless until the
        // middleware is also enabled, but the symmetry simplifies migration.
        //
        // Secret discipline: when CSRF is ENABLED, a missing/empty secret is a hard error (you cannot
        // have CSRF protection with no key) — CsrfTokenService throws, and we let it. When DISABLED
        // (e.g. dev without the secret provisioned yet), fall back to an ephemeral random key so the
        // host still BOOTS instead of crashing on an unconfigured-but-unused secret (same posture as
        // the Sentry empty-DSN guard).
        var effectiveSecret = !string.IsNullOrEmpty(secret)
            ? secret
            : (enabled ? secret : Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton(new CsrfTokenService(effectiveSecret));
        services.AddSingleton(new CsrfOptions
        {
            Enabled = enabled,
            OptOutPaths = optOutPaths,
        });
        return services;
    }

    public static IApplicationBuilder UseCsrfValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CsrfValidationMiddleware>();
    }
}
