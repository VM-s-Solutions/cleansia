using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// T-0113 (LG-SEC-05) / ADR-0001 Addendum A1 — read-parity + write-side-parity behavioural tests.
///
/// These spin a REAL <see cref="CleansiaDbContext"/> (so <see cref="CleansiaDbContext.OnModelCreating"/>
/// and the global tenant query filter actually run) over SQLite in-memory — no Postgres/Docker. They
/// reproduce the exact divergence the panel surfaced: a plan seeded the way the AUTHENTICATED subscribe
/// flow sees it (under a concrete tenant), then read back the way an ANONYMOUS caller reads it (no JWT ⇒
/// no tenant). While <see cref="MembershipPlan"/> is still <c>ITenantEntity</c> the filter collapses the
/// anonymous read to <c>TenantId == null</c> and finds nothing — RED. Once the interface is dropped the
/// type carries no tenant dimension, the filter does not apply, and both reads return the same plan — GREEN.
///
/// Written TEST-FIRST (RED before the entity change).
/// </summary>
public sealed class MembershipPlanTenantCorrectnessTests : IDisposable
{
    private const string TenantA = "tenant-A";
    private const string PlanCode = "PLUS_MONTHLY";

    private readonly SqliteConnection _connection;

    public MembershipPlanTenantCorrectnessTests()
    {
        // A single shared in-memory connection kept open for the test's lifetime = one durable schema.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Build a context bound to the shared in-memory db with the given tenant context. A null
    /// <paramref name="tenantId"/> models the ANONYMOUS caller (no JWT ⇒ no tenant_id claim ⇒
    /// <see cref="ITenantProvider.GetCurrentTenantId"/> returns null); a non-null value models the
    /// authenticated subscribe flow for that tenant.
    /// </summary>
    private CleansiaDbContext NewContext(string? tenantId)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    /// <summary>
    /// Seed one active plan exactly as the authenticated subscribe flow would: created while a concrete
    /// tenant (<see cref="TenantA"/>) is in context. While MembershipPlan is ITenantEntity,
    /// <see cref="CleansiaDbContext.CommitAsync"/> stamps <c>TenantId = TenantA</c> on insert; once it is
    /// platform config there is no TenantId to stamp. Either way this is "the tenant's plan".
    /// </summary>
    private async Task SeedActivePlanAsTenantAsync()
    {
        await using var ctx = NewContext(TenantA);
        await ctx.Database.EnsureCreatedAsync();

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus_monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);

        ctx.Add(plan);
        await ctx.CommitAsync(CancellationToken.None);
    }

    // ── AC2 — anonymous GetActivePlansAsync returns the SAME active plans as the authenticated flow ──

    [Fact]
    public async Task GetActivePlansAsync_Anonymous_ReturnsTheTenantsActivePlans_NoNullTenantCollapse()
    {
        await SeedActivePlanAsTenantAsync();

        // The authenticated subscribe flow (tenant in context) sees the plan...
        await using (var authedCtx = NewContext(TenantA))
        {
            var authed = await new MembershipPlanRepository(authedCtx).GetActivePlansAsync(CancellationToken.None);
            Assert.Single(authed);
            Assert.Equal(PlanCode, authed[0].Code);
        }

        // ...and the ANONYMOUS caller (no JWT ⇒ no tenant) must see the SAME plan. While MembershipPlan
        // was ITenantEntity the filter collapsed this to TenantId == null and returned ZERO (RED).
        await using var anonCtx = NewContext(tenantId: null);
        var anonymous = await new MembershipPlanRepository(anonCtx).GetActivePlansAsync(CancellationToken.None);

        Assert.Single(anonymous);
        Assert.Equal(PlanCode, anonymous[0].Code);
    }

    // ── AC6 — GetByCodeAsync resolves the plan regardless of tenant context (write-side parity) ──

    [Fact]
    public async Task GetByCodeAsync_ResolvesGlobalPlan_RegardlessOfTenantContext()
    {
        await SeedActivePlanAsTenantAsync();

        // Anonymous-subscribe path resolves the plan by code with NO tenant in context.
        await using (var anonCtx = NewContext(tenantId: null))
        {
            var fromAnonymous = await new MembershipPlanRepository(anonCtx).GetByCodeAsync(PlanCode, CancellationToken.None);
            Assert.NotNull(fromAnonymous);
            Assert.Equal(PlanCode, fromAnonymous!.Code);
        }

        // The tenant-overridden Stripe webhook (HandleSubscriptionEvent sets the membership's tenant)
        // must resolve the SAME global plan code — no null-tenant miss, no per-tenant mismatch.
        await using var tenantCtx = NewContext(TenantA);
        var fromTenant = await new MembershipPlanRepository(tenantCtx).GetByCodeAsync(PlanCode, CancellationToken.None);

        Assert.NotNull(fromTenant);
        Assert.Equal(PlanCode, fromTenant!.Code);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
