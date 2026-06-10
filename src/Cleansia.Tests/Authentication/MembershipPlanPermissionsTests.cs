using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// AC7 (T-0175a) — the four new membership-plan admin permissions resolve to
/// AdminOnly fail-closed under ADR-0001: a non-admin (customer/partner) JWT is
/// denied. A missing map row would resolve to PhysicalPolicy.Deny (still not
/// AdminOnly) — so this asserts the additive rows landed correctly.
/// </summary>
public class MembershipPlanPermissionsTests
{
    [Theory]
    [InlineData(Policy.CanViewMembershipPlans)]
    [InlineData(Policy.CanCreateMembershipPlan)]
    [InlineData(Policy.CanUpdateMembershipPlan)]
    [InlineData(Policy.CanDeactivateMembershipPlan)]
    public void MembershipPlanPermissions_Map_AdminOnly(string permission)
    {
        Assert.Equal(PhysicalPolicy.AdminOnly, permission.ToPhysicalPolicy());
    }

    [Fact]
    public void AssertComplete_StillPasses_AfterAdditiveRows()
    {
        // No unmapped Policy.* constant — the boot-guard reflection check passes.
        PolicyBuilder.AssertComplete();
    }
}
