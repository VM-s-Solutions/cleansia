using Cleansia.Config.Abstractions;
using Cleansia.Config.Authentication;
using Cleansia.Web.Partner.Extensions;
using Cleansia.Web.Partner.Middleware;

namespace Cleansia.Web.Partner;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaPartner";
    protected override string SwaggerTitle => "Cleansia.Partner.API v1";
    protected override Type RequestLoggingMiddlewareType => typeof(RequestLoggingMiddleware);

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
        services.AddCsrfProtection(Configuration, new[]
        {
            "/api/auth/",
            "/api/Auth/",
        });
        services.AddSingleton(new AuthCookieConfig
        {
            AccessCookieName = "partner_token",
            RefreshCookieName = "partner_refresh_token",
            RequireSecure = !Environment.IsDevelopment(),
        });
        services.AddScoped<AuthCookieWriter>();
    }

    protected override void UseHostAuthMiddleware(IApplicationBuilder app)
    {
        app.UseCsrfValidation();
    }
}
