using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Verification #1b (T-0100 / ADR-0001 §D1.2, AC6) — the AnonymousAllowList is exhaustive.
///
/// It must be neither SHORT (a legit anonymous permission missing → startup AssertComplete bricks
/// boot) nor LONG (a permission listed as anonymous while it is actually gating a real route via
/// [Permission] → a route silently un-gated). We reflect over every host's controllers.
///
/// Reality of this codebase: anonymous endpoints carry a bare <c>[AllowAnonymous]</c> and do NOT
/// carry a <c>[Permission(...)]</c>; the 7 allow-list permission *constants* are declared in
/// <see cref="Policy"/> but never attached to a route. So the exhaustiveness contract reduces to:
/// the allow-list equals exactly the declared <c>Policy.*</c> constants that are (a) not in
/// <c>PolicyBuilder.Map</c> and (b) not referenced by any <c>[Permission]</c> action on any host.
/// </summary>
public class AnonymousAllowListExhaustivenessTests
{
    /// <summary>One type per host assembly so we can enumerate every host's controllers.</summary>
    private static readonly Type[] HostMarkers =
    {
        typeof(Cleansia.Web.Admin.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Partner.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Customer.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Mobile.Partner.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Mobile.Customer.Attributes.PermissionAttribute),
    };

    /// <summary>
    /// Every Policy.* string referenced by a [Permission(...)] (i.e. an AuthorizeAttribute subclass
    /// whose Policy was produced by <c>ToPhysicalPolicy()</c>) on any controller action across hosts.
    /// We collect the *logical* permission by reverse-mapping the physical policy is impossible, so
    /// instead we read the constructor argument captured on the attribute instance.
    /// </summary>
    private static HashSet<string> PermissionsReferencedByRoutes()
    {
        var referenced = new HashSet<string>();

        // Build a reverse index physical→logical is ambiguous; instead we detect [Permission] usage
        // by scanning method custom-attribute data for the host PermissionAttribute and reading its
        // single string constructor argument (the logical Policy.* name).
        foreach (var marker in HostMarkers)
        {
            var asm = marker.Assembly;
            var permissionAttrType = marker;

            foreach (var type in asm.GetTypes())
            {
                if (!IsController(type))
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var data in method.GetCustomAttributesData())
                    {
                        if (data.AttributeType != permissionAttrType)
                            continue;
                        if (data.ConstructorArguments.Count == 1 &&
                            data.ConstructorArguments[0].Value is string permission)
                        {
                            referenced.Add(permission);
                        }
                    }
                }
            }
        }

        return referenced;
    }

    private static bool IsController(Type type) =>
        type.IsClass && !type.IsAbstract &&
        (type.Name.EndsWith("Controller", StringComparison.Ordinal) ||
         type.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ApiControllerAttribute>() != null ||
         typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type));

    private static HashSet<string> DeclaredPolicyConstants() =>
        typeof(Policy)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

    private static HashSet<string> MapKeys()
    {
        var field = typeof(PolicyBuilder).GetField("Map",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var map = (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
        return map.Keys.ToHashSet();
    }

    [Fact]
    public void AllowList_Equals_Unmapped_And_Route_Unreferenced_Policy_Constants()
    {
        var declared = DeclaredPolicyConstants();
        var mapped = MapKeys();
        var routeReferenced = PermissionsReferencedByRoutes();

        // Permissions that are neither mapped nor used by any [Permission] route — these are the
        // only ones legitimately left to be AllowAnonymous.
        var unmappedAndUnreferenced = declared
            .Except(mapped)
            .Except(routeReferenced)
            .OrderBy(x => x)
            .ToHashSet();

        var allowList = PolicyBuilder.AnonymousAllowList.OrderBy(x => x).ToHashSet();

        var missingFromAllowList = unmappedAndUnreferenced.Except(allowList).OrderBy(x => x).ToList();
        var extraInAllowList = allowList.Except(unmappedAndUnreferenced).OrderBy(x => x).ToList();

        Assert.True(
            missingFromAllowList.Count == 0 && extraInAllowList.Count == 0,
            "AnonymousAllowList is not exhaustive vs. the host controllers.\n" +
            $"Anonymous permissions MISSING from the allow-list (boot would brick): {string.Join(", ", missingFromAllowList)}\n" +
            $"Allow-list entries that are NOT genuinely anonymous (route un-gated risk): {string.Join(", ", extraInAllowList)}");
    }

    [Fact]
    public void No_AllowList_Entry_Is_Actively_Gating_A_Route_Via_Permission()
    {
        // The "long" failure mode, stated directly: nothing on the allow-list may also be attached
        // to a [Permission] action.
        var routeReferenced = PermissionsReferencedByRoutes();
        var contradictions = PolicyBuilder.AnonymousAllowList.Intersect(routeReferenced).OrderBy(x => x).ToList();
        Assert.True(contradictions.Count == 0,
            $"These allow-list permissions are actively gating routes via [Permission]: {string.Join(", ", contradictions)}");
    }
}
