using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Every routed action on every host makes an authorization decision, and the decision is visible in
/// the diff that adds it.
///
/// <para><b>Why this is not covered by what already exists.</b> The base
/// <c>CleansiaApiController</c> carries a bare <c>[Authorize]</c>, whose default policy is
/// "authenticated, JWT scheme" and <b>nothing else</b> (<c>ServiceExtensions.cs:41-46</c>). So an
/// action written without <c>[Permission(...)]</c> is not un-routed and not 401 — it is reachable by
/// <b>any authenticated caller of any role</b>, which on the Admin host means a customer's token
/// opens an admin route. <see cref="AnonymousAllowListExhaustivenessTests"/> checks the *anonymous*
/// direction (the allow-list is neither short nor long); it says nothing about actions that carry no
/// authorization attribute at all. The only thing pinning admin routes to admins today is a handful
/// of named <c>Cleansia.HostTests</c> cases — one per route someone thought to write — so route
/// number N+1 is unpinned by construction.</para>
///
/// <para><b>The facts.</b> The bare-authenticated bucket across all five hosts equals a frozen
/// roster, so a new entry is a deliberate line in a diff rather than an omission; the Admin subset of
/// it is asserted separately because that is where the bucket is dangerous. The roster is a list and
/// not a count on purpose: a count stays green when one route leaves the bucket and another enters
/// it. All twenty-six current entries were read and justified, not grandfathered — they are the
/// three shapes for which "signed in" genuinely is the whole question: signing yourself out,
/// platform reference data that belongs to nobody, and customer money paths that resolve the subject
/// from the caller's own token and refuse a non-owner inside the request.</para>
/// </summary>
public class EveryRouteCarriesAnAuthorizationDecisionTests
{
    /// <summary>One type per host assembly, mirroring <see cref="AnonymousAllowListExhaustivenessTests"/>.</summary>
    private static readonly (string Host, Type Marker)[] HostMarkers =
    {
        ("Admin", typeof(Cleansia.Web.Admin.Attributes.PermissionAttribute)),
        ("Partner", typeof(Cleansia.Web.Partner.Attributes.PermissionAttribute)),
        ("Customer", typeof(Cleansia.Web.Customer.Attributes.PermissionAttribute)),
        ("Mobile.Partner", typeof(Cleansia.Web.Mobile.Partner.Attributes.PermissionAttribute)),
        ("Mobile.Customer", typeof(Cleansia.Web.Mobile.Customer.Attributes.PermissionAttribute)),
    };

    /// <summary>
    /// Routes that gate on nothing but "you are signed in", written out so that adding a
    /// twenty-seventh is an edit to this list rather than an omission. Each entry is
    /// <c>Host/Controller.Action</c>, and each group carries the reason bare authentication is the
    /// right answer for it. Nothing here is grandfathered silently — a route that could not be
    /// justified would have been fixed instead of listed.
    /// </summary>
    private static readonly HashSet<string> BareAuthenticatedRoster = new(StringComparer.Ordinal)
    {
        // (a) Signing yourself out. Requiring a role to end your own session would strand a caller
        //     whose role changed mid-session — the one moment logout matters most.
        "Admin/AdminAuthController.Logout",
        "Customer/AuthController.Logout",
        "Mobile.Customer/AuthController.Logout",
        "Mobile.Partner/AuthController.Logout",
        "Partner/AuthController.Logout",

        // (b) Platform reference data — countries, languages, currencies, serviced cities, the
        //     service/package catalogue, and the enum catalogue the admin UI binds its dropdowns to
        //     (GetCodeOverview returns domain enum names off the assembly, no rows at all:
        //     GetCodeOverview.cs:13). No row is anyone's, so there is nothing to scope to a caller.
        //     GetFieldLabels joins them: a country's own word for its registration number is a
        //     property of the jurisdiction, not of the caller — and the partner registration form
        //     that renders it already sits behind the same sign-in.
        "Admin/AdminCodeController.GetOverview",
        "Mobile.Customer/CountryController.GetOverview",
        "Mobile.Customer/CountryController.GetServiced",
        "Mobile.Customer/LanguageController.GetOverview",
        "Mobile.Customer/ServiceCityController.GetServiceCities",
        "Mobile.Partner/CountryController.GetFieldLabels",
        "Mobile.Partner/CountryController.GetOverview",
        "Mobile.Partner/CountryController.GetServiced",
        "Mobile.Partner/LanguageController.GetOverview",
        "Mobile.Partner/ServiceCityController.GetServiceCities",
        "Partner/CountryController.GetFieldLabels",
        "Partner/CountryController.GetOverview",
        "Partner/CountryController.GetServiced",
        "Partner/CurrencyController.GetOverview",
        "Partner/LanguageController.GetOverview",
        "Partner/PackageController.GetOverview",
        "Partner/ServiceController.GetOverview",

        // (c) Customer money paths that resolve the subject from the caller's own token and refuse a
        //     non-owner INSIDE the request. A role policy would not help — the question is not "what
        //     kind of caller" but "is this your order", which only the handler can answer.
        //     CreatePaymentIntent.cs:39-42 (BeOwnedByCallerAsync, deliberately answering
        //     OrderNotFound to a non-owner so ids cannot be enumerated) and
        //     ConfirmRecurringOrder.cs:81 (order.UserId != sessionUserId).
        "Customer/OrderController.ConfirmRecurring",
        "Customer/PaymentController.CreatePaymentIntent",
        "Mobile.Customer/OrderController.ConfirmRecurring",
        "Mobile.Customer/PaymentController.CreatePaymentIntent",
    };

    private sealed record Route(string Host, string Controller, string Action)
    {
        public override string ToString() => $"{Host}/{Controller}.{Action}";
    }

    private static bool IsController(Type type) =>
        type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type);

    private static bool IsRoutedAction(MethodInfo method) =>
        !method.IsSpecialName &&
        method.GetCustomAttributes().OfType<IActionHttpMethodProvider>().Any();

    /// <summary>
    /// An action's authorization decision, walking method then declaring type then base types —
    /// which is what ASP.NET Core itself does, and why the base <c>[Authorize]</c> counts.
    /// </summary>
    private static (bool Anonymous, bool HasPolicyOrRoles, bool HasBareAuthorize) Classify(MethodInfo method)
    {
        var anonymous = method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null
                        || method.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null;

        var authorizeAttributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .ToList();

        var hasPolicyOrRoles = authorizeAttributes.Any(a =>
            !string.IsNullOrEmpty(a.Policy) || !string.IsNullOrEmpty(a.Roles));

        return (anonymous, hasPolicyOrRoles, authorizeAttributes.Count > 0);
    }

    private static IEnumerable<(Route Route, bool Anonymous, bool HasPolicyOrRoles, bool HasBareAuthorize)> AllRoutes()
    {
        foreach (var (host, marker) in HostMarkers)
        {
            foreach (var type in marker.Assembly.GetTypes().Where(IsController))
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!IsRoutedAction(method))
                        continue;

                    var (anonymous, hasPolicyOrRoles, hasBareAuthorize) = Classify(method);
                    yield return (new Route(host, type.Name, method.Name), anonymous, hasPolicyOrRoles, hasBareAuthorize);
                }
            }
        }
    }

    // ── Anti-vacuity ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_Scan_Reaches_Every_Host_And_A_Real_Number_Of_Routes()
    {
        var routes = AllRoutes().ToList();

        // A reflection scan that silently matches nothing passes every other assertion in this file.
        Assert.True(routes.Count > 200, $"only {routes.Count} routed actions found across five hosts");

        foreach (var (host, _) in HostMarkers)
        {
            Assert.True(routes.Any(r => r.Route.Host == host), $"no routed actions found on host {host}");
        }

        // And the classifier distinguishes: all three buckets are non-empty somewhere, so a
        // classifier that collapsed to one answer would fail here rather than pass silently.
        Assert.Contains(routes, r => r.Anonymous);
        Assert.Contains(routes, r => r.HasPolicyOrRoles);
    }

    // ── Fact #1 — the Admin host's bare-authenticated set is exactly two, both of them roleless
    //             by nature ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called out separately from the all-host roster because the Admin host is where this bucket is
    /// sharpest: an admin route with no policy is opened by a customer's token, since the base
    /// <c>[Authorize]</c>'s default policy asks only for authentication. Exactly two admin routes are
    /// in it, and neither carries a row — see the roster's groups (a) and (b). A third is a defect
    /// until someone argues otherwise here.
    /// </summary>
    [Fact]
    public void The_Only_Admin_Routes_On_Bare_Authentication_Are_Logout_And_The_Enum_Catalogue()
    {
        var expected = new[]
        {
            "Admin/AdminAuthController.Logout",
            "Admin/AdminCodeController.GetOverview",
        };

        var actual = AllRoutes()
            .Where(r => r.Route.Host == "Admin" && !r.Anonymous && !r.HasPolicyOrRoles)
            .Select(r => r.Route.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);
    }

    // ── Fact #2 — the bare-authenticated roster is frozen across all hosts ──────────────────────

    [Fact]
    public void The_Bare_Authenticated_Roster_Is_Exactly_The_Frozen_List()
    {
        var actual = AllRoutes()
            .Where(r => !r.Anonymous && !r.HasPolicyOrRoles)
            .Select(r => r.Route.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var added = actual.Except(BareAuthenticatedRoster).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var removed = BareAuthenticatedRoster.Except(actual).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(added.Count == 0 && removed.Count == 0,
            "The set of routes gated on bare authentication has changed.\n"
            + $"NEW (a route that gates on nothing but being signed in — is that intended?):\n  {string.Join("\n  ", added)}\n"
            + $"GONE (delete from the roster):\n  {string.Join("\n  ", removed)}");
    }

    // ── Every route makes SOME decision ─────────────────────────────────────────────────────────

    [Fact]
    public void No_Route_Escapes_Authorization_Entirely()
    {
        var escaped = AllRoutes()
            .Where(r => !r.Anonymous && !r.HasBareAuthorize)
            .Select(r => r.Route.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(escaped.Count == 0,
            "These routes carry neither [AllowAnonymous] nor any [Authorize] on the action, the "
            + "controller or a base type — they are anonymous by omission rather than by decision:\n  "
            + string.Join("\n  ", escaped));
    }
}
