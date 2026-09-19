using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// AC7 (T-0175a) — the four membership-plan admin permissions resolve to an administrator set,
/// fail-closed under ADR-0001: a non-admin (customer/partner) JWT is denied. The read is any
/// administrator's and the writes are the Manager's (ADR-0066 D3). A missing map row would resolve to
/// PhysicalPolicy.Deny — so this asserts the rows landed correctly.
/// </summary>
public class MembershipPlanPermissionsTests
{
    [Fact]
    public void MembershipPlanRead_Maps_AnyAdministrator()
    {
        Assert.Equal(PhysicalPolicy.AdminOnly, Policy.CanViewMembershipPlans.ToPhysicalPolicy());
    }

    [Theory]
    [InlineData(Policy.CanCreateMembershipPlan)]
    [InlineData(Policy.CanUpdateMembershipPlan)]
    [InlineData(Policy.CanDeactivateMembershipPlan)]
    public void MembershipPlanWrites_Map_ManagerOrAbove(string permission)
    {
        Assert.Equal(PhysicalPolicy.ManagerOrAbove, permission.ToPhysicalPolicy());
    }

    [Fact]
    public void AssertComplete_StillPasses_AfterAdditiveRows()
    {
        // No unmapped Policy.* constant — the boot-guard reflection check passes.
        PolicyBuilder.AssertComplete();
    }
}
