using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Services;

public static class ServiceExtensions
{
    /// <summary>
    /// The single shared authorization registration for ALL hosts (ADR-0001 §D4). Sets the default
    /// policy (authenticated + JWT scheme), registers every physical policy exactly once — including
    /// <see cref="PhysicalPolicy.CustomerOnly"/> on every host and the fail-closed
    /// <see cref="PhysicalPolicy.Deny"/> sentinel — and wires the startup filter that runs
    /// <see cref="PolicyBuilder.AssertComplete"/> (at filter construction) plus presence + behavior
    /// assertions (in <c>Configure</c>, where the built provider is available).
    /// Replaces the five hand-copied per-host <c>AddUserAuthorization</c> bodies and the legacy
    /// duplicate <c>AddJwt "AdminOnly"</c> registration.
    /// </summary>
    public static IServiceCollection AddCleansiaAuthorization(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .Build())
            .AddPolicy(PhysicalPolicy.Authenticated,
                p => p.RequireAuthenticatedUser())
            // Customer = authenticated user that is NOT Employee/Admin (no dedicated role claim — the
            // absence of those roles is what makes someone a customer). Registered on EVERY host so a
            // CustomerOnly endpoint routed onto Admin/Partner resolves and correctly denies an admin.
            .AddPolicy(PhysicalPolicy.CustomerOnly,
                p => p.RequireAssertion(ctx =>
                {
                    var user = ctx.User;
                    return user.Identity?.IsAuthenticated == true
                        && !user.IsInRole(UserProfile.Employee.ToString())
                        && !user.IsInRole(UserProfile.Administrator.ToString());
                }))
            .AddPolicy(PhysicalPolicy.EmployeeOrAdmin,
                p => p.RequireRole(
                    UserProfile.Employee.ToString(),
                    UserProfile.Administrator.ToString()))
            .AddPolicy(PhysicalPolicy.AdminOnly,
                p => p.RequireRole(UserProfile.Administrator.ToString()))
            // OwnerOrElevated (ADR-0001 §D3): elevated == Admin ONLY (the old blanket
            // IsInRole(Employee) → true grant was an employee-wide PII IDOR and is removed). A
            // non-admin caller is allowed IFF the requested subject id equals their own sub. The id is
            // read from one canonical helper (route "id" → route "userId" → query "UserId") because the
            // sole consumer (Web.Partner UserController.GetById) supplies it as the query param
            // "UserId", which the old RouteValues["id"]-only read never matched. A non-HttpContext
            // resource fails closed for a non-admin (deny — never an over-grant); the handler ownership
            // gate (GetUser.Handler) is the inner backstop on any non-MVC path.
            .AddPolicy(PhysicalPolicy.OwnerOrElevated, p =>
                p.RequireAssertion(ctx =>
                {
                    var user = ctx.User;

                    if (user.IsInRole(UserProfile.Administrator.ToString()))
                        return true; // elevated = Admin ONLY

                    if (ctx.Resource is not HttpContext http)
                        return false; // fail-closed (availability note)

                    var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var requestedId = ResolveSubjectId(http);
                    return requestedId is not null && requestedId == sub; // owner
                }))
            // Fail-closed sentinel — an unmapped permission resolves here and always 403s.
            .AddPolicy(PhysicalPolicy.Deny, p => p.RequireAssertion(_ => false));

        services.AddSingleton<IStartupFilter, AuthorizationCompletenessStartupFilter>();

        return services;
    }

    /// <summary>
    /// The single canonical resolver for the subject id an <see cref="PhysicalPolicy.OwnerOrElevated"/>
    /// endpoint is about (ADR-0001 §D3). Checks, in order, the route value <c>id</c>, the route value
    /// <c>userId</c>, then the query value <c>UserId</c>. The controller contract is frozen to those
    /// names so every future OwnerOrElevated endpoint stays resolvable. Returns <c>null</c> when none
    /// is present (the caller then denies a non-admin — fail-closed).
    /// </summary>
    private static string? ResolveSubjectId(HttpContext http)
    {
        if (http.Request.RouteValues.TryGetValue("id", out var routeId) &&
            routeId?.ToString() is { Length: > 0 } byRouteId)
            return byRouteId;

        if (http.Request.RouteValues.TryGetValue("userId", out var routeUserId) &&
            routeUserId?.ToString() is { Length: > 0 } byRouteUserId)
            return byRouteUserId;

        var byQuery = http.Request.Query["UserId"].ToString();
        return byQuery.Length > 0 ? byQuery : null;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IFiscalRetryService, FiscalRetryService>();
        // ADR-0002 D3.4 — the dispatch-layer reconciliation sweep (DISTINCT
        // from the registration-retry FiscalRetryService above; they are not merged).
        services.AddScoped<IFiscalReconciliationService, FiscalReconciliationService>();
        // The single dedicated outbox drainer (claim under a lease → send → mark dispatched). Resolved
        // by the one timer-triggered drainer host; not run per API/Functions instance.
        services.AddScoped<IOutboxDrainerService, OutboxDrainerService>();
        services.AddScoped<IAppConfigurationProvider, AppConfigurationProvider>();
        services.AddScoped<ITaxIdValidator, TaxIdValidator>();
        services.AddScoped<IVatCalculator, VatCalculator>();
        services.AddScoped<ICurrencyResolutionService, CurrencyResolutionService>();
        services.AddScoped<IOrderPricingCalculator, OrderPricingCalculator>();
        services.AddScoped<IOrderFactory, OrderFactory>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IPromoCodeService, PromoCodeService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddScoped<IStripeSubscriptionWebhookHandler, StripeSubscriptionWebhookHandler>();
        services.AddScoped<ICancellationPolicyResolver, CancellationPolicyResolver>();
        services.AddScoped<IOrderAccessService, OrderAccessService>();
        services.AddScoped<IAddressGeocoder, AddressGeocoder>();
        services.AddScoped<IGdprDeletionService, GdprDeletionService>();
        services.AddScoped<IGdprExportService, GdprExportService>();
        services.AddInfrastructureServices();

        return services;
    }
}