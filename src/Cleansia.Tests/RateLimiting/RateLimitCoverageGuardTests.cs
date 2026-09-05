using System.Reflection;
using Microsoft.AspNetCore.Mvc;
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
        // ADR-0034 D8.5: the payout reveal is modelled as a POST command precisely so this guard reaches
        // it. Masking converts "one query returns everything" into "N deliberate reveals", which is only
        // a control if N is bounded — the same policy that lists employee ids grants the reveal route.
        typeof(Cleansia.Web.Admin.Controllers.AdminEmployeeController),
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

    /// <summary>
    /// Every controller in the five host assemblies, found by reflection rather than by memory.
    ///
    /// <para>The list above is the contract; THIS is the thing that notices a controller that never
    /// made it onto the list. A hand-enumerated roster fails OPEN: a new money endpoint on a new
    /// controller is not weakly covered by it, it is invisible to it, and the suite stays green.</para>
    /// </summary>
    private static IEnumerable<Type> AllHostControllers() =>
        new[]
        {
            typeof(Cleansia.Web.Customer.Controllers.PaymentController).Assembly,
            typeof(Cleansia.Web.Mobile.Customer.Controllers.PaymentController).Assembly,
            typeof(Cleansia.Web.Partner.Controllers.PaymentController).Assembly,
            typeof(Cleansia.Web.Mobile.Partner.Controllers.OrderController).Assembly,
            typeof(Cleansia.Web.Admin.Controllers.AdminAuthController).Assembly,
        }
        .SelectMany(a => a.GetTypes())
        .Where(t => t is { IsAbstract: false, IsClass: true }
            && typeof(ControllerBase).IsAssignableFrom(t)
            && MutatingActionsOf(t).Any());

    /// <summary>
    /// Mutating actions that carry NO rate-limit window today.
    ///
    /// <para><b>These are gaps, not decisions.</b> Nothing here has been ruled safe — the list exists
    /// so the sweep below can be fail-CLOSED about everything else while the owner decides what to do
    /// about these 50. It is a baseline that may only ever SHRINK, which is
    /// the same shape check-module-boundaries uses for its known violations.</para>
    ///
    /// <para>Several are worth reading rather than skimming: <c>AdminUserController.CreateAdminUser</c>
    /// mints an administrator, <c>AdminPromoCodeController.CreatePromoCode</c> mints money, and both
    /// <c>SavedAddressController</c> pairs are customer-facing writes on an authenticated surface.</para>
    /// </summary>
    private static readonly HashSet<string> KnownUncovered = new(StringComparer.Ordinal)
    {
        "Cleansia.Web.Admin.Controllers.AdminCompanyController.CreateCompanyInfo",
        "Cleansia.Web.Admin.Controllers.AdminCompanyController.UpdateCompanyInfo",
        "Cleansia.Web.Admin.Controllers.AdminCompanyController.DeleteCompanyInfo",
        "Cleansia.Web.Admin.Controllers.AdminCountryController.CreateCountry",
        "Cleansia.Web.Admin.Controllers.AdminCountryController.UpdateCountry",
        "Cleansia.Web.Admin.Controllers.AdminCountryController.DeleteCountry",
        "Cleansia.Web.Admin.Controllers.AdminCountryController.SetServiced",
        "Cleansia.Web.Admin.Controllers.AdminCurrencyController.CreateCurrency",
        "Cleansia.Web.Admin.Controllers.AdminCurrencyController.UpdateCurrency",
        "Cleansia.Web.Admin.Controllers.AdminCurrencyController.DeleteCurrency",
        "Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController.GetPagedDocuments",
        "Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController.ApproveDocument",
        "Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController.RejectDocument",
        "Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController.SaveRequirement",
        "Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController.DeleteRequirement",
        "Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController.ResolveDeletionRequest",
        "Cleansia.Web.Admin.Controllers.AdminFeatureFlagController.Create",
        "Cleansia.Web.Admin.Controllers.AdminFeatureFlagController.Toggle",
        "Cleansia.Web.Admin.Controllers.AdminFeatureFlagController.Delete",
        "Cleansia.Web.Admin.Controllers.AdminFiscalFailureController.RetryFiscalRegistration",
        "Cleansia.Web.Admin.Controllers.AdminFiscalFailureController.AcknowledgeFiscalFailure",
        "Cleansia.Web.Admin.Controllers.AdminLanguageController.CreateLanguage",
        "Cleansia.Web.Admin.Controllers.AdminLanguageController.UpdateLanguage",
        "Cleansia.Web.Admin.Controllers.AdminLanguageController.DeleteLanguage",
        "Cleansia.Web.Admin.Controllers.AdminLoyaltyTierController.UpdateTierConfig",
        "Cleansia.Web.Admin.Controllers.AdminLoyaltyTierController.PreviewThresholdImpact",
        "Cleansia.Web.Admin.Controllers.AdminPackageController.CreatePackage",
        "Cleansia.Web.Admin.Controllers.AdminPackageController.UpdatePackage",
        "Cleansia.Web.Admin.Controllers.AdminPackageController.DeletePackage",
        "Cleansia.Web.Admin.Controllers.AdminPromoCodeController.CreatePromoCode",
        "Cleansia.Web.Admin.Controllers.AdminPromoCodeController.UpdatePromoCode",
        "Cleansia.Web.Admin.Controllers.AdminPromoCodeController.DeactivatePromoCode",
        "Cleansia.Web.Admin.Controllers.AdminServiceCityController.CreateServiceCity",
        "Cleansia.Web.Admin.Controllers.AdminServiceCityController.UpdateServiceCity",
        "Cleansia.Web.Admin.Controllers.AdminServiceCityController.DeleteServiceCity",
        "Cleansia.Web.Admin.Controllers.AdminServiceController.CreateService",
        "Cleansia.Web.Admin.Controllers.AdminServiceController.UpdateService",
        "Cleansia.Web.Admin.Controllers.AdminServiceController.DeleteService",
        "Cleansia.Web.Admin.Controllers.AdminUserController.CreateAdminUser",
        "Cleansia.Web.Admin.Controllers.AdminUserController.UpdateAdminUser",
        "Cleansia.Web.Admin.Controllers.AdminUserController.DeactivateAdminUser",
        "Cleansia.Web.Admin.Controllers.AdminUserController.ActivateAdminUser",
        "Cleansia.Web.Customer.Controllers.SavedAddressController.Add",
        "Cleansia.Web.Customer.Controllers.SavedAddressController.SetDefault",
        "Cleansia.Web.Customer.Controllers.SavedAddressController.Update",
        "Cleansia.Web.Customer.Controllers.SavedAddressController.Delete",
        "Cleansia.Web.Mobile.Customer.Controllers.SavedAddressController.Add",
        "Cleansia.Web.Mobile.Customer.Controllers.SavedAddressController.SetDefault",
        "Cleansia.Web.Mobile.Customer.Controllers.SavedAddressController.Update",
        "Cleansia.Web.Mobile.Customer.Controllers.SavedAddressController.Delete",
    };

    /// <summary>
    /// Every mutating action in every host, not just the ones somebody remembered to list.
    ///
    /// <para>The roster above is a hand-written array. That shape fails OPEN: a money endpoint on a
    /// controller nobody added is not weakly covered by it, it is invisible to it, and the suite
    /// stays green. Nineteen controllers had already drifted off it. This finds them by reflection
    /// instead, so the only way a new uncovered action can pass is if someone writes it into
    /// <see cref="KnownUncovered"/> — a deliberate act with a name attached, not an omission.</para>
    /// </summary>
    [Fact]
    public void No_Mutating_Action_In_Any_Host_Is_Silently_Unlimited()
    {
        var newlyUncovered = new List<string>();

        foreach (var controller in AllHostControllers())
        foreach (var action in MutatingActionsOf(controller))
        {
            var disabled = action.GetCustomAttribute<DisableRateLimitingAttribute>() is not null;
            if (!disabled && EffectivePolicyOf(action) is not null)
            {
                continue;
            }

            var id = $"{controller.FullName}.{action.Name}";
            if (!KnownUncovered.Contains(id))
            {
                newlyUncovered.Add(id);
            }
        }

        Assert.True(newlyUncovered.Count == 0,
            "S5 — these mutating actions carry no rate-limit window and are not in the recorded "
            + "baseline. Add [EnableRateLimiting], or add them to KnownUncovered with a reason:\n  "
            + string.Join("\n  ", newlyUncovered.OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The baseline may only shrink. Without this an entry could outlive the action it names, and the
    /// list would quietly become fiction.
    /// </summary>
    [Fact]
    public void The_Uncovered_Baseline_Contains_Nothing_Stale()
    {
        var live = AllHostControllers()
            .SelectMany(c => MutatingActionsOf(c).Select(a => $"{c.FullName}.{a.Name}"))
            .ToHashSet(StringComparer.Ordinal);

        var stale = KnownUncovered.Where(id => !live.Contains(id))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(stale.Count == 0,
            "These KnownUncovered entries name actions that no longer exist. Remove them:\n  "
            + string.Join("\n  ", stale));
    }

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

    // ADR-0039 D12.1: MyServingCleaners is a READ, so neither the mutating sweep above nor S5 as
    // written reaches it — and that is exactly why it shipped unthrottled while CancelOrder eleven
    // lines above it carries a window. Its answer is per-subject and repeating it reconstructs a
    // named cleaner's work calendar, so it is rate-limited like a side-effecting endpoint.
    [Theory]
    [InlineData(typeof(Cleansia.Web.Customer.Controllers.OrderController), "MyServingCleaners")]
    [InlineData(typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController), "MyServingCleaners")]
    public void MyServingCleaners_Carries_The_Auth_Window(Type controller, string action)
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
