using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Cleansia.Core.AppServices.Auditing;
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
        typeof(Cleansia.Web.Partner.Controllers.PayPeriodController),
        typeof(Cleansia.Web.Partner.Controllers.EmployeePayrollController),
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
    /// Mutating actions deliberately left without a rate-limit window.
    ///
    /// <para><b>Empty, and that is the point.</b> It held fifty entries — every mutating action the
    /// old hand-written roster had drifted past, including CreateAdminUser, CreatePromoCode and both
    /// SavedAddressController pairs. All fifty now carry a window, so the set emptied rather than
    /// being maintained. Anything added here in future is a decision with a name attached.</para>
    /// </summary>
    private static readonly HashSet<string> KnownUncovered = new(StringComparer.Ordinal)
    {
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

    // ── ADR-0062 D1 / S5: a customer-marked command is a row-writer ──────────

    /// <summary>
    /// The two hosts a customer or a guest reaches. A customer-marked command dispatched from an
    /// unlimited route is a storage amplifier: every call, refused or not, writes a row.
    /// </summary>
    private static IEnumerable<Type> CustomerHostControllers() =>
        new[]
        {
            typeof(Cleansia.Web.Customer.Controllers.OrderController).Assembly,
            typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController).Assembly,
        }
        .SelectMany(a => a.GetTypes())
        .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(t));

    private static readonly HashSet<Type> CustomerMarkedCommands = typeof(IAuditContext).Assembly
        .GetTypes()
        .Where(t => t.GetCustomAttribute<AuditActionAttribute>(inherit: false) is { Audience: AuditAudience.Customer })
        .Select(t => t.GetNestedType("Command")!)
        .ToHashSet();

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    /// <summary>
    /// An action dispatches a marked command when it binds one as a parameter or constructs one in its
    /// body. The body of an async action is the compiler-generated state machine, so its <c>MoveNext</c>
    /// is what gets read; the scan is for <c>newobj</c> tokens that resolve to a marked command.
    /// </summary>
    private static IReadOnlyList<Type> MarkedCommandsDispatchedBy(MethodInfo action)
    {
        var bound = action.GetParameters().Select(p => p.ParameterType).Where(CustomerMarkedCommands.Contains);
        var body = action.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            .GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? action;
        return bound.Concat(ConstructedTypesIn(body).Where(CustomerMarkedCommands.Contains)).Distinct().ToList();
    }

    private static IEnumerable<Type> ConstructedTypesIn(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var module = method.Module;
        var genericArguments = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
        for (var i = 0; i < il.Length;)
        {
            var code = il[i] == 0xFE ? TwoByteOpCodes[il[i + 1]] : OneByteOpCodes[il[i]];
            i += code.Size;
            var operandSize = OperandSizeOf(code, il, i);
            if (code == OpCodes.Newobj)
            {
                Type? constructed = null;
                try
                {
                    constructed = module.ResolveMethod(BitConverter.ToInt32(il, i), genericArguments, null)?.DeclaringType;
                }
                catch (ArgumentException)
                {
                }

                if (constructed is not null)
                {
                    yield return constructed;
                }
            }

            i += operandSize;
        }
    }

    private static int OperandSizeOf(OpCode code, byte[] il, int operandStart) => code.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, operandStart),
        _ => 4,
    };

    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static RateLimitCoverageGuardTests()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var code = (OpCode)field.GetValue(null)!;
            if (code.Size == 1)
            {
                OneByteOpCodes[code.Value & 0xFF] = code;
            }
            else
            {
                TwoByteOpCodes[code.Value & 0xFF] = code;
            }
        }
    }

    [Fact]
    public void Every_Customer_Host_Action_That_Dispatches_A_Customer_Marked_Command_Carries_A_RateLimit_Window()
    {
        var unlimited = new List<string>();

        foreach (var controller in CustomerHostControllers())
        foreach (var action in ActionsOf(controller))
        {
            var dispatched = MarkedCommandsDispatchedBy(action);
            if (dispatched.Count == 0)
            {
                continue;
            }

            var disabled = action.GetCustomAttribute<DisableRateLimitingAttribute>() is not null;
            if (disabled || EffectivePolicyOf(action) is null)
            {
                unlimited.Add($"{controller.FullName}.{action.Name} -> {string.Join(", ", dispatched.Select(t => t.DeclaringType!.Name))}");
            }
        }

        Assert.True(unlimited.Count == 0,
            "ADR-0062 D1 / S5: a customer-marked command writes a CustomerActionAudits row on every call, "
            + "refused or not. These customer-host actions dispatch one without a window:\n  "
            + string.Join("\n  ", unlimited));
    }

    /// <summary>
    /// Anti-vacuity for the guard above: every marked command is dispatched by some customer-host action
    /// and every marked LABEL is reachable from BOTH customer hosts — the password sign-in is two commands
    /// (the web one keeps the trusted-device marker off the wire, the mobile one carries it) sharing one
    /// label, so the by-command check is per host family and the by-label check is per host. A guard that
    /// found none would be a broken scan, not a clean surface.
    /// </summary>
    [Fact]
    public void Every_Customer_Marked_Command_Is_Dispatched_On_A_Customer_Host_And_Every_Label_On_Both()
    {
        Assert.NotEmpty(CustomerMarkedCommands);

        var dispatchedAnywhere = CustomerHostControllers()
            .SelectMany(ActionsOf)
            .SelectMany(MarkedCommandsDispatchedBy)
            .ToHashSet();
        var unreachable = CustomerMarkedCommands.Except(dispatchedAnywhere).Select(t => t.FullName).OrderBy(x => x).ToList();
        Assert.True(unreachable.Count == 0,
            "no customer-host action dispatches these customer-marked commands (or the IL scan missed a "
            + "construction site):\n  " + string.Join("\n  ", unreachable));

        var labels = CustomerMarkedCommands.Select(LabelOf).ToHashSet();
        foreach (var host in new[] { typeof(Cleansia.Web.Customer.Controllers.OrderController).Assembly, typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController).Assembly })
        {
            var dispatched = CustomerHostControllers()
                .Where(c => c.Assembly == host)
                .SelectMany(ActionsOf)
                .SelectMany(MarkedCommandsDispatchedBy)
                .Select(LabelOf)
                .ToHashSet();

            var missing = labels.Except(dispatched).OrderBy(x => x).ToList();
            Assert.True(missing.Count == 0,
                $"{host.GetName().Name}: no action dispatches a command carrying these customer labels:\n  "
                + string.Join("\n  ", missing));
        }
    }

    private static string LabelOf(Type command) => AuditActionDescriptor.For(command).Action;

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
    [InlineData(typeof(Cleansia.Web.Admin.Controllers.AdminCountryController), "SetDefaultMarket")]
    public void Catalog_Lifecycle_Action_Keeps_Its_Auth_Window(Type controller, string action)
    {
        Assert.Equal("auth", EffectivePolicyOf(controller.GetMethod(action)!));
    }
}
