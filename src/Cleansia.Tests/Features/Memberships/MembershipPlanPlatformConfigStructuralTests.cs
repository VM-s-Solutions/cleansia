using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// ADR-0001 Addendum A1 — MANDATORY anti-regression test ("no footgun").
///
/// THE HOLE: <see cref="MembershipPlan"/> implemented <see cref="ITenantEntity"/>, so the EF Core
/// global tenant query filter applied to it. On the <c>[AllowAnonymous] GetPlans</c> route there is no
/// JWT, so the tenant claim is null and the filter collapsed to <c>TenantId == null</c> — the public
/// marketing page returned only the null-tenant slice (wrong/empty), and a future <c>TenantId == null</c>
/// "shared" plan row would leak to EVERY tenant's anonymous page.
///
/// THE FIX (ratified — ADR-0001 A1): MembershipPlan becomes platform config (like Currency / Language /
/// Country, which are already NOT <see cref="ITenantEntity"/>). Dropping the interface removes the tenant
/// dimension entirely — there is no <c>TenantId</c> to collapse, so the footgun is structurally gone.
///
/// This is the structural guard the architect mandated: it asserts the type does NOT carry the tenant
/// dimension, so a future "let's re-add ITenantEntity for tenant-specific plan catalogues" change can't
/// silently re-open the anonymous-read hole without a deliberate ticket (it would turn this test RED).
///
/// Written TEST-FIRST: it fails RED while <see cref="MembershipPlan"/> still implements
/// <see cref="ITenantEntity"/>, and goes GREEN the moment the interface is dropped.
/// </summary>
public class MembershipPlanPlatformConfigStructuralTests
{
    [Fact]
    public void MembershipPlan_IsNot_ITenantEntity_SoItHasNoTenantDimension()
    {
        // the dimension is gone. No TenantId ⇒ nothing for the global filter to collapse on an
        // anonymous read ⇒ no null-tenant slice, no cross-tenant "shared row" leak.
        Assert.False(
            typeof(ITenantEntity).IsAssignableFrom(typeof(MembershipPlan)),
            "MembershipPlan must NOT implement ITenantEntity — it is platform config (ADR-0001 Addendum A1). "
            + "Re-adding the tenant dimension re-opens the anonymous-read hole and must go through "
            + "a deliberate ticket, not slip in unnoticed.");
    }

    [Fact]
    public void MembershipPlan_MatchesPlatformConfigPrecedent_AuditableButNotITenantEntity()
    {
        // Exact precedent parity with Currency (already platform config — Auditable, NOT ITenantEntity).
        // Note: the TenantId property itself lives on the Auditable base and is shared by ALL entities
        // (Currency has it too); what makes something tenant-scoped is the ITenantEntity MARKER, which is
        // what the EF global filter (CleansiaDbContext.ApplyTenantQueryFilters) keys off. So the contract
        // is "is Auditable, is NOT ITenantEntity" — the dormant TenantId column is the owner's migration
        // to drop, not a C#-visible difference.
        Assert.True(typeof(Auditable).IsAssignableFrom(typeof(MembershipPlan)));
        Assert.False(typeof(ITenantEntity).IsAssignableFrom(typeof(MembershipPlan)));

        // The platform-config precedent the panel cited (Currency) has exactly this shape.
        Assert.True(typeof(Auditable).IsAssignableFrom(typeof(Currency)));
        Assert.False(typeof(ITenantEntity).IsAssignableFrom(typeof(Currency)));
    }

    [Fact]
    public void LoyaltyTierConfig_StaysTenantScoped_NoAnonymousReadPath_AC4()
    {
        // LoyaltyTierConfig is the same shape but has NO anonymous read path, so it stays
        // ITenantEntity (untouched by this ticket). This pins that decision: if someone drops the
        // interface from LoyaltyTierConfig too, that is a SEPARATE decision and this test catches it.
        Assert.True(
            typeof(ITenantEntity).IsAssignableFrom(typeof(LoyaltyTierConfig)),
            "LoyaltyTierConfig must remain ITenantEntity — it has no anonymous read path, so the platform-config carve-out does not apply.");
    }
}
