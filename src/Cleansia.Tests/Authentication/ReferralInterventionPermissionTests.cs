using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// The referral-intervention permission resolves to Support's set (ADR-0066 D3: a
/// reverse / force-qualify is a per-customer intervention like credit), fail-closed under ADR-0001: a
/// non-admin (customer/partner) JWT is denied. A missing map row would resolve to PhysicalPolicy.Deny.
/// </summary>
public class ReferralInterventionPermissionTests
{
    [Fact]
    public void CanInterveneReferral_Maps_SupportOrAbove()
    {
        Assert.Equal(PhysicalPolicy.SupportOrAbove, Policy.CanInterveneReferral.ToPhysicalPolicy());
    }

    [Fact]
    public void AssertComplete_StillPasses_AfterAdditiveRow()
    {
        PolicyBuilder.AssertComplete();
    }
}
