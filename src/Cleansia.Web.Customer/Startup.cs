using Cleansia.Config.Abstractions;
using Cleansia.Config.Authentication;
using Cleansia.Web.Customer.Extensions;
using Cleansia.Web.Customer.Middleware;

namespace Cleansia.Web.Customer;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaCustomer";
    protected override string SwaggerTitle => "Cleansia.Customer.API v1";
    protected override Type RequestLoggingMiddlewareType => typeof(RequestLoggingMiddleware);

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
        // CSRF opt-out paths: Stripe webhook (own signature), the auth
        // endpoints themselves (no session yet at sign-in / refresh time),
        // and the anonymous order lookup family.
        services.AddCsrfProtection(Configuration, new[]
        {
            "/api/payments/webhook",
            "/api/auth/",
            "/api/Auth/",
            "/api/order/Lookup",
            "/api/order/LookupBatch",
            "/api/order/Quote",
            "/api/order/CreateOrder",
        });
        services.AddSingleton(new AuthCookieConfig
        {
            AccessCookieName = "customer_token",
            RefreshCookieName = "customer_refresh_token",
            RequireSecure = !Environment.IsDevelopment(),
        });
        services.AddScoped<AuthCookieWriter>();
    }

    protected override void UseHostAuthMiddleware(IApplicationBuilder app)
    {
        app.UseCsrfValidation();
    }
}
