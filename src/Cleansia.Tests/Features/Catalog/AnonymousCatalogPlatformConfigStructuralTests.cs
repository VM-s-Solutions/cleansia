using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// ADR-0001 Addendum A1 (D-A1.1 doctrine) anti-regression guard for the five anonymous-readable
/// catalog entities. These are served on <c>[AllowAnonymous]</c> customer/mobile routes, so an
/// anonymous request carries no <c>tenant_id</c> claim. While they implemented <see cref="ITenantEntity"/>
/// the EF global tenant filter applied and collapsed every anonymous read to the <c>TenantId == null</c>
/// slice (wrong/empty under multi-tenant, and a shared null-tenant row would leak to every tenant).
///
/// Dropping the interface removes the tenant dimension entirely — they become platform config in the
/// same bucket as <see cref="Currency"/>/<see cref="Language"/>/<c>Country</c>. This test pins that
/// classification so re-adding <see cref="ITenantEntity"/> (which would re-open the anonymous-read hole)
/// can only happen through a deliberate ticket, not slip in unnoticed (it turns this test RED).
/// </summary>
public class AnonymousCatalogPlatformConfigStructuralTests
{
    public static TheoryData<Type> CatalogEntities =>
    [
        typeof(Service),
        typeof(ServiceCategory),
        typeof(Package),
        typeof(Extra),
        typeof(ServiceCity),
    ];

    [Theory]
    [MemberData(nameof(CatalogEntities))]
    public void CatalogEntity_IsNot_ITenantEntity(Type entityType)
    {
        Assert.False(
            typeof(ITenantEntity).IsAssignableFrom(entityType),
            $"{entityType.Name} must NOT implement ITenantEntity — it is anonymous-readable platform config "
            + "(ADR-0001 Addendum A1). Re-adding the tenant dimension re-opens the anonymous-read collapse "
            + "and must go through a deliberate ticket.");
    }

    [Theory]
    [MemberData(nameof(CatalogEntities))]
    public void CatalogEntity_MatchesPlatformConfigPrecedent_AuditableButNotITenantEntity(Type entityType)
    {
        // Same shape as Currency, the platform-config precedent the panel cited: Auditable (the dormant
        // TenantId column lives on that base, shared by all entities), but NOT ITenantEntity — the marker
        // the global filter (CleansiaDbContext.ApplyTenantQueryFilters) keys off.
        Assert.True(typeof(Auditable).IsAssignableFrom(entityType));
        Assert.False(typeof(ITenantEntity).IsAssignableFrom(entityType));

        Assert.True(typeof(Auditable).IsAssignableFrom(typeof(Currency)));
        Assert.False(typeof(ITenantEntity).IsAssignableFrom(typeof(Currency)));
    }
}
