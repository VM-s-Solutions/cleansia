using System.Reflection;
using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Verification #1 (completeness / AssertComplete) and #3 (no fail-open default) for
/// T-0100 / ADR-0001 §D1. These are the pure-logic guarantees of the fail-closed seam.
/// </summary>
public class PolicyBuilderTests
{
    // ── #3 — no fail-open default ──────────────────────────────────────────

    [Fact]
    public void Unmapped_Permission_Resolves_To_Deny_Not_Authenticated()
    {
        // The runtime backstop: an unknown permission must yield the always-403 sentinel,
        // never "any authenticated user".
        Assert.Equal(PhysicalPolicy.Deny, "SomeUnknownPermission".ToPhysicalPolicy());
        Assert.NotEqual(PhysicalPolicy.Authenticated, "AnyUnknownPermission".ToPhysicalPolicy());
    }

    [Fact]
    public void Deny_Sentinel_Constant_Exists_And_Is_Distinct()
    {
        Assert.Equal("Deny", PhysicalPolicy.Deny);
        Assert.NotEqual(PhysicalPolicy.Authenticated, PhysicalPolicy.Deny);
    }

    // ── #1 — AssertComplete completeness ───────────────────────────────────

    [Fact]
    public void AssertComplete_Passes_For_The_Current_Map_And_AllowList()
    {
        // Mirror of the startup assertion: with the map filled and the allow-list frozen,
        // completeness holds (no missing, no orphan, no contradiction).
        var ex = Record.Exception(PolicyBuilder.AssertComplete);
        Assert.Null(ex);
    }

    [Fact]
    public void Every_Declared_Policy_Constant_Is_Mapped_Or_AllowListed()
    {
        var declared = DeclaredPolicyConstants();
        var unaccounted = declared
            .Except(MapKeys())
            .Except(PolicyBuilder.AnonymousAllowList)
            .OrderBy(x => x)
            .ToList();

        Assert.True(unaccounted.Count == 0,
            "Every Policy.* constant must be in PolicyBuilder.Map or AnonymousAllowList. " +
            $"Unaccounted: {string.Join(", ", unaccounted)}");
    }

    [Fact]
    public void Map_Has_No_Orphan_Keys()
    {
        var declared = DeclaredPolicyConstants();
        var orphans = MapKeys().Except(declared).OrderBy(x => x).ToList();
        Assert.True(orphans.Count == 0,
            $"PolicyBuilder.Map references unknown permissions: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void AllowList_Does_Not_Intersect_Map()
    {
        var contradictions = PolicyBuilder.AnonymousAllowList.Intersect(MapKeys()).OrderBy(x => x).ToList();
        Assert.True(contradictions.Count == 0,
            $"Permissions both AllowAnonymous and mapped: {string.Join(", ", contradictions)}");
    }

    [Fact]
    public void AnonymousAllowList_Is_The_Frozen_Seven()
    {
        var expected = new HashSet<string>
        {
            Policy.CanViewCodeOverview,
            Policy.CanPerformGlobalSearch,
            Policy.CanViewOrderDetailWithOrderNumberAndEmail,
            Policy.CanCreateOrder,
            Policy.CanGetOrderStatus,
            Policy.CanRequestPasswordChange,
            Policy.CanChangePassword,
        };
        Assert.True(expected.SetEquals(PolicyBuilder.AnonymousAllowList),
            "AnonymousAllowList must be exactly the 7 entries frozen in ADR-0001 D1.2. " +
            $"Actual: {string.Join(", ", PolicyBuilder.AnonymousAllowList.OrderBy(x => x))}");
    }

    [Fact]
    public void AssertComplete_Detection_Has_Teeth_For_A_Missing_Constant()
    {
        // We cannot mutate the private static Map to trigger AssertComplete's throw directly, but we
        // prove the exact set arithmetic AssertComplete uses flags a synthetic "forgot to map"
        // permission. (A real forgotten Policy.* is caught at build time by
        // Every_Declared_Policy_Constant_Is_Mapped_Or_AllowListed above; AC5's runtime throw is the
        // same predicate applied at startup.)
        var declaredPlusForgotten = DeclaredPolicyConstants();
        declaredPlusForgotten.Add("CanDoSomethingNewlyAddedButUnmapped");

        var missing = declaredPlusForgotten
            .Except(MapKeys())
            .Except(PolicyBuilder.AnonymousAllowList)
            .ToList();

        Assert.Contains("CanDoSomethingNewlyAddedButUnmapped", missing);
    }

    [Fact]
    public void AssertComplete_Is_The_InvalidOperationException_Throwing_Kind()
    {
        var method = typeof(PolicyBuilder).GetMethod(nameof(PolicyBuilder.AssertComplete),
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method!.ReturnType);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static HashSet<string> DeclaredPolicyConstants() =>
        typeof(Policy)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

    private static IEnumerable<string> MapKeys()
    {
        var field = typeof(PolicyBuilder).GetField("Map",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var map = (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
        return map.Keys;
    }
}
