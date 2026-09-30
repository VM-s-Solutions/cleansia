using Cleansia.Config.Abstractions;
using Cleansia.Config.Authentication;
using Cleansia.Web.Customer.Extensions;
using Cleansia.Web.Customer.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

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
        // A lapsed session would otherwise run as a guest on the routes that serve both, and book
        // without the account, its consent or its member price. The 401 makes the client refresh and
        // replay; the auth routes are exempt because the refresh arrives carrying the lapsed token.
        // Reads are exempt too: nothing is written as a guest, and a browser <img> such as the
        // booking map sends the cookie without passing through the client's refresh interceptor.
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method)
                && !HttpMethods.IsOptions(context.Request.Method)
                && !context.Request.Path.StartsWithSegments("/api/Auth")
                && (await context.AuthenticateAsync()).Failure is SecurityTokenExpiredException)
            {
                await context.ChallengeAsync();
                return;
            }

            await next(context);
        });
        app.UseCsrfValidation();
    }
}
