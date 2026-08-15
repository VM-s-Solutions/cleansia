using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// EF-MODEL-METADATA guard for the recurring-occurrence arbiter (G-18).
///
/// THE HOLE: <c>MaterializeRecurringBookingTemplate</c> decides "did we already spawn THIS occurrence"
/// with an UNLOCKED read, and <c>Order.RecurringTemplateId</c> carried only a NON-unique index. Two
/// overlapping invocations would both read "not materialized" and both insert — a duplicate order, and
/// for a card template a duplicate charge against the customer.
///
/// What actually prevented that was outside the code entirely: Azure Functions timer triggers hold a
/// singleton lease, so <c>MaterializeRecurringBookingsFunction</c>'s <c>[TimerTrigger]</c> could not run
/// twice concurrently. The behaviour was correct and the guarantee lived in the HOSTING MODEL — move the
/// sweep to any other scheduler, or fan it out, and duplicate billing returns with nothing to catch it.
///
/// THE FIX (asserted here): a UNIQUE index over <c>(RecurringTemplateId, CleaningDateTime)</c> — the
/// exact key the handler reasons with, both sides whole minutes produced by <c>ComputeOccurrences</c>,
/// so equality asks the handler's own question and nothing wider.
///
/// <para><b>The filter is load-bearing twice over.</b> Every one-off order carries
/// <c>RecurringTemplateId</c> NULL, and without the filter they would contend with each other. It also
/// keeps <c>TenantId</c> out of the key: a unique index containing nullable <c>TenantId</c> enforces
/// nothing in single-tenant mode, because Postgres treats NULLs as distinct — the landmine
/// <c>CLAUDE.md</c> names, and the reason this index is two non-null columns rather than three.</para>
/// </summary>
public sealed class RecurringOccurrenceIndexTests
{
    private static IEntityType GetOrderEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(Order));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static IIndex? OccurrenceIndex() =>
        GetOrderEntityType()
            .GetIndexes()
            .SingleOrDefault(i =>
                i.Properties.Count == 2
                && i.Properties[0].Name == nameof(Order.RecurringTemplateId)
                && i.Properties[1].Name == nameof(Order.CleaningDateTime));

    [Fact]
    public void The_Occurrence_Index_Exists_And_Is_Unique()
    {
        var index = OccurrenceIndex();

        Assert.True(
            index is not null,
            "No index over (RecurringTemplateId, CleaningDateTime). Without it the only thing stopping a "
            + "duplicate recurring order — and a duplicate charge — is the Functions singleton lease.");
        Assert.True(
            index!.IsUnique,
            "The occurrence index must be UNIQUE. A non-unique one indexes the question without answering it.");
    }

    /// <summary>
    /// Filtered to spawned orders. Every one-off order has a NULL template id, and an unfiltered unique
    /// index would make them contend — the whole platform would accept exactly one order per instant.
    /// </summary>
    [Fact]
    public void The_Occurrence_Index_Excludes_One_Off_Orders()
    {
        var filter = OccurrenceIndex()!.GetFilter();

        Assert.False(
            string.IsNullOrWhiteSpace(filter),
            "The index must be filtered to RecurringTemplateId IS NOT NULL, or every one-off order "
            + "collides with every other order at the same instant.");
        Assert.Contains("RecurringTemplateId", filter!);
        Assert.Contains("NOT NULL", filter!);
    }

    /// <summary>
    /// TenantId must stay OUT of this key. It is nullable and NULL in single-tenant mode — production
    /// today — and Postgres treats NULLs as distinct, so a key containing it would admit unlimited
    /// duplicates while appearing to arbitrate. → /architecture/security-rules
    /// </summary>
    [Fact]
    public void The_Occurrence_Index_Does_Not_Depend_On_The_Nullable_TenantId()
    {
        var names = OccurrenceIndex()!.Properties.Select(p => p.Name).ToList();

        Assert.DoesNotContain("TenantId", names);
    }

    /// <summary>Mirrors the seat-index tests' provider (null ⇒ single-tenant, which is production).</summary>
    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
