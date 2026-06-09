using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Refunds;

public sealed class RefundModelMetadataTests
{
    private static IEntityType GetRefundEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(Refund));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static bool IsSingleColumnIndexOn(IIndex index, string propertyName) =>
        index.Properties.Count == 1 && index.Properties[0].Name == propertyName;

    [Fact]
    public void Refund_IsMappedToTheRefundsTable()
    {
        var refund = GetRefundEntityType();
        Assert.Equal("Refunds", refund.GetTableName());
    }

    [Fact]
    public void Refund_IsTenantScoped()
    {
        Assert.True(typeof(ITenantEntity).IsAssignableFrom(typeof(Refund)));

        var refund = GetRefundEntityType();
        Assert.NotNull(refund.GetQueryFilter());
    }

    [Fact]
    public void RefundKey_HasExactlyOneSingleColumnUniqueIndex()
    {
        var refund = GetRefundEntityType();

        var keyIndexes = refund.GetIndexes()
            .Where(ix => IsSingleColumnIndexOn(ix, nameof(Refund.RefundKey)))
            .ToList();

        Assert.Single(keyIndexes);
        Assert.True(
            keyIndexes[0].IsUnique,
            "Refund.RefundKey must carry a single-column UNIQUE index so a concurrent double-issue "
            + "collapses on 23505 and the seam resolves to the existing refund instead of double-sending.");
    }

    [Fact]
    public void Refund_HasForeignKeyToOrder()
    {
        var refund = GetRefundEntityType();

        var orderFk = refund.GetForeignKeys()
            .SingleOrDefault(fk => fk.Properties.Count == 1
                && fk.Properties[0].Name == nameof(Refund.OrderId));

        Assert.NotNull(orderFk);
        Assert.False(orderFk!.Properties[0].IsNullable);
    }

    [Fact]
    public void Refund_WindowOverrideReason_IsNullable_WithMaxLength500()
    {
        var refund = GetRefundEntityType();

        var property = refund.FindProperty(nameof(Refund.WindowOverrideReason));
        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(500, property.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(Refund.ReceiptId))]
    [InlineData(nameof(Refund.DisputeId))]
    public void Refund_HasNullableForeignKey(string foreignKeyProperty)
    {
        var refund = GetRefundEntityType();

        var fk = refund.GetForeignKeys()
            .SingleOrDefault(f => f.Properties.Count == 1
                && f.Properties[0].Name == foreignKeyProperty);

        Assert.NotNull(fk);
        Assert.True(
            fk!.Properties[0].IsNullable,
            $"Refund.{foreignKeyProperty} links the refund to a Receipt/Dispute only when one applies, "
            + "so the FK must be nullable.");
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
