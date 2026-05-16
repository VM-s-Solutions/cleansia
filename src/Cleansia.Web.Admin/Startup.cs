using Cleansia.Config.Abstractions;
using Cleansia.Config.Authentication;
using Cleansia.Web.Admin.Extensions;
using Cleansia.Web.Admin.Middleware;

namespace Cleansia.Web.Admin;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaAdmin";
    protected override string SwaggerTitle => "Cleansia.Admin.API v1";
    protected override Type RequestLoggingMiddlewareType => typeof(RequestLoggingMiddleware);

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
        services.AddCsrfProtection(Configuration, new[]
        {
            "/api/AdminAuth/",
            "/api/auth/",
        });
        services.AddSingleton(new AuthCookieConfig
        {
            AccessCookieName = "admin_token",
            RefreshCookieName = "admin_refresh_token",
            RequireSecure = !Environment.IsDevelopment(),
        });
        services.AddScoped<AuthCookieWriter>();
    }

    protected override void UseHostAuthMiddleware(IApplicationBuilder app)
    {
        app.UseCsrfValidation();
    }
}
