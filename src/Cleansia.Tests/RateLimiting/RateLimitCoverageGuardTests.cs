using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// S5 coverage guard (ADR-0003 / BSP-4d) — the structural complement to
/// <see cref="RateLimiterSourceContractTests"/>. That class proves the limiter is *registered*
/// correctly; this one proves the money/side-effect surface actually *uses* it: every mutating
/// action (POST/PUT/DELETE/PATCH) on the enumerated controllers must carry
/// <c>[EnableRateLimiting]</c> (action- or class-level), so a future endpoint on these controllers
/// cannot ship without a window. Reflection over the host assemblies, mirroring
/// <see cref="Cleansia.Tests.Authentication.AnonymousAllowListExhaustivenessTests"/>.
/// </summary>
public class RateLimitCoverageGuardTests
{
    /// <summary>
    /// Known policy names from CleansiaStartupBase/RateLimitPolicies. A typo'd policy name on an
    /// attribute is worse than no attribute (it throws at request time), so the guard pins the set.
    /// </summary>
    private static readonly string[] KnownPolicies = { "auth", "interactive", "webhook" };

    /// <summary>
    /// The money/side-effect controllers (ticket-enumerated + the auth surfaces already covered
    /// class-level). Contract: every mutating action carries a rate-limit window. Read/list
    /// actions (GET) are deliberately NOT asserted — reads carry no window by convention.
    /// </summary>
    private static readonly Type[] MoneyAndSideEffectControllers =
    {
        // Customer host
        typeof(Cleansia.Web.Customer.Controllers.AuthController),
        typeof(Cleansia.Web.Customer.Controllers.PaymentController),
        typeof(Cleansia.Web.Customer.Controllers.MembershipController),
        typeof(Cleansia.Web.Customer.Controllers.DisputeController),
        typeof(Cleansia.Web.Customer.Controllers.RecurringBookingController),
        typeof(Cleansia.Web.Customer.Controllers.DeviceController),
        typeof(Cleansia.Web.Customer.Controllers.ReferralController),
        typeof(Cleansia.Web.Customer.Controllers.PromoCodeController),
        typeof(Cleansia.Web.Customer.Controllers.UserController),
        typeof(Cleansia.Web.Customer.Controllers.OrderController),
        typeof(Cleansia.Web.Customer.Controllers.GdprController),
        typeof(Cleansia.Web.Customer.Controllers.NotificationPreferencesController),

        // Mobile.Customer host (same audience surface as Customer)
        typeof(Cleansia.Web.Mobile.Customer.Controllers.AuthController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.PaymentController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.MembershipController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.DisputeController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.RecurringBookingController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.DeviceController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.ReferralController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.PromoCodeController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.UserController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.GdprController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.NotificationPreferencesController),

        // Partner host
        typeof(Cleansia.Web.Partner.Controllers.AuthController),
        typeof(Cleansia.Web.Partner.Controllers.PaymentController),
        typeof(Cleansia.Web.Partner.Controllers.PayConfigController),
        typeof(Cleansia.Web.Partner.Controllers.PayPeriodController),
        typeof(Cleansia.Web.Partner.Controllers.EmployeePayrollController),
        typeof(Cleansia.Web.Partner.Controllers.DisputeController),
        typeof(Cleansia.Web.Partner.Controllers.UserController),
        typeof(Cleansia.Web.Partner.Controllers.OrderController),
        typeof(Cleansia.Web.Partner.Controllers.EmployeeController),
        typeof(Cleansia.Web.Partner.Controllers.GdprController),

        // Mobile.Partner host
        typeof(Cleansia.Web.Mobile.Partner.Controllers.AuthController),
        typeof(Cleansia.Web.Mobile.Partner.Controllers.DeviceController),
        typeof(Cleansia.Web.Mobile.Partner.Controllers.OrderController),
        typeof(Cleansia.Web.Mobile.Partner.Controllers.EmployeeController),
        typeof(Cleansia.Web.Mobile.Partner.Controllers.GdprController),

        // Admin host (payroll, disputes, order ops, refunds, membership, referral, loyalty)
        typeof(Cleansia.Web.Admin.Controllers.AdminAuthController),
        typeof(Cleansia.Web.Admin.Controllers.AdminPayrollController),
        typeof(Cleansia.Web.Admin.Controllers.AdminPayPeriodController),
        typeof(Cleansia.Web.Admin.Controllers.AdminDisputeController),
        typeof(Cleansia.Web.Admin.Controllers.AdminOrderController),
        typeof(Cleansia.Web.Admin.Controllers.AdminRefundController),
        typeof(Cleansia.Web.Admin.Controllers.AdminMembershipController),
        typeof(Cleansia.Web.Admin.Controllers.AdminReferralController),
        typeof(Cleansia.Web.Admin.Controllers.AdminLoyaltyController),
        typeof(Cleansia.Web.Admin.Controllers.AdminInvoiceController),
        typeof(Cleansia.Web.Admin.Controllers.AdminPayConfigController),
        typeof(Cleansia.Web.Admin.Controllers.AdminMarketingController),
        typeof(Cleansia.Web.Admin.Controllers.AdminEmailTemplateController),
        typeof(Cleansia.Web.Admin.Controllers.AdminGdprController),
    };

    private static readonly string[] MutatingMethods = { "POST", "PUT", "DELETE", "PATCH" };

    private static IEnumerable<MethodInfo> MutatingActionsOf(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Any(a => a.HttpMethods.Any(v => MutatingMethods.Contains(v, StringComparer.OrdinalIgnoreCase))));

    private static string? EffectivePolicyOf(MethodInfo action) =>
        action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName
        ?? action.DeclaringType!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

    [Fact]
    public void Every_Mutating_Action_On_Money_Controllers_Carries_A_RateLimit_Window()
    {
        var uncovered = new List<string>();

        foreach (var controller in MoneyAndSideEffectControllers)
        foreach (var action in MutatingActionsOf(controller))
        {
            var disabled = action.GetCustomAttribute<DisableRateLimitingAttribute>() is not null;
            if (disabled || EffectivePolicyOf(action) is null)
                uncovered.Add($"{controller.FullName}.{action.Name}");
        }

        Assert.True(uncovered.Count == 0,
            "S5 (ADR-0003/BSP-4d): every POST/PUT/DELETE/PATCH on a money/side-effect controller " +
            "must carry [EnableRateLimiting] (action- or class-level). Uncovered actions:\n  " +
            string.Join("\n  ", uncovered));
    }

    [Fact]
    public void Every_RateLimit_Window_On_Money_Controllers_Uses_A_Registered_Policy_Name()
    {
        var unknown = new List<string>();

        foreach (var controller in MoneyAndSideEffectControllers)
        foreach (var action in MutatingActionsOf(controller))
        {
            var policy = EffectivePolicyOf(action);
            if (policy is not null && !KnownPolicies.Contains(policy))
                unknown.Add($"{controller.FullName}.{action.Name} -> \"{policy}\"");
        }

        Assert.True(unknown.Count == 0,
            "An unregistered rate-limit policy name throws at request time. Unknown policies:\n  " +
            string.Join("\n  ", unknown));
    }

    // AC5 — webhooks keep their dedicated per-source-IP policy; a 429 from "auth"/"interactive"
    // would read to Stripe as a retry trigger.
    [Theory]
    [InlineData(typeof(Cleansia.Web.Customer.Controllers.PaymentController), "Webhook")]
    [InlineData(typeof(Cleansia.Web.Mobile.Customer.Controllers.PaymentController), "Webhook")]
    [InlineData(typeof(Cleansia.Web.Partner.Controllers.PaymentController), "Webhook")]
    public void Stripe_Webhook_Keeps_The_Dedicated_Webhook_Policy(Type controller, string action)
    {
        Assert.Equal("webhook", EffectivePolicyOf(controller.GetMethod(action)!));
    }

    // NotificationPreferences GetMine is a side-effecting GET (it lazy-creates the prefs row on first
    // call), so the mutating-method sweep above does not reach it. T-0350: it carries the "auth" window
    // explicitly on BOTH hosts — pin it directly.
    [Theory]
    [InlineData(typeof(Cleansia.Web.Customer.Controllers.NotificationPreferencesController), "GetMine")]
    [InlineData(typeof(Cleansia.Web.Mobile.Customer.Controllers.NotificationPreferencesController), "GetMine")]
    public void NotificationPreferences_GetMine_Carries_The_Auth_Window(Type controller, string action)
    {
        Assert.Equal("auth", EffectivePolicyOf(controller.GetMethod(action)!));
    }

    // Catalog lifecycle (partially covered controllers, so not in the full-coverage list):
    // the lifecycle actions themselves must keep their window.
    [Theory]
    [InlineData(typeof(Cleansia.Web.Admin.Controllers.AdminServiceController), "DeactivateService")]
    [InlineData(typeof(Cleansia.Web.Admin.Controllers.AdminServiceController), "ActivateService")]
    [InlineData(typeof(Cleansia.Web.Admin.Controllers.AdminPackageController), "DeactivatePackage")]
    [InlineData(typeof(Cleansia.Web.Admin.Controllers.AdminPackageController), "ActivatePackage")]
    [InlineData(typeof(Cleansia.Web.Admin.Controllers.AdminCurrencyController), "SetDefaultCurrency")]
    public void Catalog_Lifecycle_Action_Keeps_Its_Auth_Window(Type controller, string action)
    {
        Assert.Equal("auth", EffectivePolicyOf(controller.GetMethod(action)!));
    }
}
