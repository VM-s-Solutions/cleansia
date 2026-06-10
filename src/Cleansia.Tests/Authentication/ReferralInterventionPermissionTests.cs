using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// AC4 (T-0176) — the new referral-intervention permission resolves to AdminOnly
/// fail-closed under ADR-0001: a non-admin (customer/partner) JWT is denied. A
/// missing map row would resolve to PhysicalPolicy.Deny (still not AdminOnly).
/// </summary>
public class ReferralInterventionPermissionTests
{
    [Fact]
    public void CanInterveneReferral_Maps_AdminOnly()
    {
        Assert.Equal(PhysicalPolicy.AdminOnly, Policy.CanInterveneReferral.ToPhysicalPolicy());
    }

    [Fact]
    public void AssertComplete_StillPasses_AfterAdditiveRow()
    {
        PolicyBuilder.AssertComplete();
    }
}
