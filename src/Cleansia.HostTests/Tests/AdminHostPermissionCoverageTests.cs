using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Web.Admin.Attributes;
using Cleansia.Web.Admin.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0066 D9.2 — every action on every admin-host controller carries a <c>[Permission]</c> that maps
/// to a non-<c>Deny</c> physical policy, or sits on the allow-list below with its reason. The list
/// fails when it names an action that no longer exists, so it cannot rot into an open door; an action
/// with no attribute at all fails by name, so a new route cannot ship un-gated. Pure reflection: no
/// host, no database.
/// </summary>
public sealed class AdminHostPermissionCoverageTests
{
    /// <summary>
    /// The actions that carry no <c>[Permission]</c> on purpose. Sign-in and refresh are anonymous by
    /// nature; sign-out and the enum overview are any signed-in administrator's — the profile gate at
    /// the mint is what keeps a customer off this host, and neither hands anything role-specific back.
    /// </summary>
    private static readonly IReadOnlyDictionary<(Type Controller, string Action), string> AllowList =
        new Dictionary<(Type, string), string>
        {
            [(typeof(AdminAuthController), nameof(AdminAuthController.Login))] = "[AllowAnonymous]: the sign-in",
            [(typeof(AdminAuthController), nameof(AdminAuthController.RefreshToken))] = "[AllowAnonymous]: the refresh, pinned by the refresh-token row and the admin audience",
            [(typeof(AdminAuthController), nameof(AdminAuthController.Logout))] = "[Authorize]: any signed-in administrator may end their own session",
            [(typeof(AdminCodeController), nameof(AdminCodeController.GetOverview))] = "[Authorize] on the controller: the enum overview hands back no data of the company's",
        };

    private static IEnumerable<Type> AdminControllers() =>
        typeof(AdminAuthController).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name);

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .OrderBy(m => m.Name);

    private static string? PermissionOf(MethodInfo action) =>
        action.GetCustomAttributesData()
            .Where(d => d.AttributeType == typeof(PermissionAttribute))
            .Select(d => d.ConstructorArguments[0].Value as string)
            .FirstOrDefault();

    [Fact]
    public void Every_admin_host_action_is_gated_by_a_mapped_permission_or_allow_listed_with_a_reason()
    {
        var offenders = new List<string>();

        foreach (var controller in AdminControllers())
        {
            foreach (var action in ActionsOf(controller))
            {
                var permission = PermissionOf(action);
                var allowListed = AllowList.ContainsKey((controller, action.Name));

                if (permission is null && !allowListed)
                {
                    offenders.Add($"{controller.Name}.{action.Name}: no [Permission] and not allow-listed");
                    continue;
                }

                if (permission is not null && allowListed)
                {
                    offenders.Add($"{controller.Name}.{action.Name}: carries [Permission({permission})] and is allow-listed — remove one");
                    continue;
                }

                if (permission is not null && permission.ToPhysicalPolicy() == PhysicalPolicy.Deny)
                {
                    offenders.Add($"{controller.Name}.{action.Name}: [Permission({permission})] resolves to Deny — unmapped");
                }

                if (allowListed && !action.GetCustomAttributes<AllowAnonymousAttribute>().Any()
                    && !action.GetCustomAttributes<AuthorizeAttribute>().Any()
                    && !controller.GetCustomAttributes<AuthorizeAttribute>().Any())
                {
                    offenders.Add($"{controller.Name}.{action.Name}: allow-listed but carries neither [AllowAnonymous] nor [Authorize]");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Admin-host actions outside the permission seam:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void The_allow_list_names_only_actions_that_exist()
    {
        var stale = AllowList.Keys
            .Where(key => ActionsOf(key.Controller).All(a => a.Name != key.Action))
            .Select(key => $"{key.Controller.Name}.{key.Action}")
            .ToList();

        Assert.True(stale.Count == 0, "Allow-list entries naming no action: " + string.Join(", ", stale));
    }

    [Fact]
    public void The_allow_list_is_the_anonymous_sign_in_routes_the_sign_out_and_the_enum_overview_only()
    {
        Assert.Equal(4, AllowList.Count);
        Assert.All(AllowList.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    [Fact]
    public void No_admin_controller_references_a_partner_host_shared_read()
    {
        string[] sharedReads =
        [
            Policy.CanViewPagedOrder, Policy.CanViewOrderDetail, Policy.CanViewOrderPhotos,
            Policy.CanViewEmployeeDocuments, Policy.CanViewEmployeePayoutDetails,
            Policy.CanViewPagedInvoices, Policy.CanViewPayPeriods, Policy.CanViewPayPeriod,
        ];

        var referenced = AdminControllers()
            .SelectMany(c => ActionsOf(c).Select(a => (Controller: c.Name, Action: a.Name, Permission: PermissionOf(a))))
            .Where(x => x.Permission is not null && sharedReads.Contains(x.Permission))
            .Select(x => $"{x.Controller}.{x.Action} → {x.Permission}")
            .ToList();

        Assert.True(referenced.Count == 0, "Admin-host actions still routed on a partner-host shared read: " + string.Join(", ", referenced));
    }
}
