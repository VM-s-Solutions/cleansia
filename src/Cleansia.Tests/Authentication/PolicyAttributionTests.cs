using System.Reflection;
using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Every <see cref="Policy"/> constant outside <see cref="PolicyBuilder.AnonymousAllowList"/> gates at
/// least one route. A constant carried only by the map, the frozen table and the web mirror is a
/// permission nobody can be refused — it reads as a security decision and decides nothing, and the
/// mirror spec keeps the two sides equal without asking whether either side is used. Eight such
/// constants accumulated before this guard existed; <c>AnonymousAllowListExhaustivenessTests</c> is
/// the same walk from the other direction.
/// </summary>
public class PolicyAttributionTests
{
    private static readonly Type[] HostMarkers =
    {
        typeof(Cleansia.Web.Admin.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Partner.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Customer.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Mobile.Partner.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Mobile.Customer.Attributes.PermissionAttribute),
    };

    private static HashSet<string> PermissionsCarriedByRoutes()
    {
        var carried = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permissionAttribute in HostMarkers)
        {
            var controllers = permissionAttribute.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t));
            foreach (var controller in controllers)
            {
                var attributed = controller.GetCustomAttributesData()
                    .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .SelectMany(m => m.GetCustomAttributesData()));
                foreach (var data in attributed)
                {
                    if (data.AttributeType == permissionAttribute
                        && data.ConstructorArguments.Count == 1
                        && data.ConstructorArguments[0].Value is string permission)
                    {
                        carried.Add(permission);
                    }
                }
            }
        }
        return carried;
    }

    private static IReadOnlyList<string> DeclaredPolicyConstants() =>
        typeof(Policy)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void The_Scan_Reaches_Every_Host_And_A_Real_Number_Of_Permissions()
    {
        var carried = PermissionsCarriedByRoutes();

        // A reflection scan that matches nothing would fail the assertion below on every constant,
        // which is loud; the silent failure mode is a scan that matches only one host.
        Assert.True(carried.Count > 100, $"only {carried.Count} permissions found on the five hosts' routes");
        Assert.Contains(Policy.CanViewAdminUsers, carried);
        Assert.Contains(Policy.CanTakeOrder, carried);
        Assert.Contains(Policy.CanManageSavedAddresses, carried);
    }

    [Fact]
    public void Every_Policy_Constant_Outside_The_Anonymous_Allow_List_Gates_A_Route()
    {
        var carried = PermissionsCarriedByRoutes();

        var unattributed = DeclaredPolicyConstants()
            .Where(p => !PolicyBuilder.AnonymousAllowList.Contains(p))
            .Where(p => !carried.Contains(p))
            .ToList();

        Assert.True(unattributed.Count == 0,
            "Policy constants no [Permission(...)] on any of the five hosts carries — a permission that "
            + "gates nothing is dead code wearing a security label; delete it (with its PolicyBuilder.Map row, "
            + "its FrozenPermissionMapTests row and its policy.ts mirror rows) or attach it to the route it "
            + "was written for:\n  " + string.Join("\n  ", unattributed));
    }
}
