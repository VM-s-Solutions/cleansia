using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.LiveActivities;

/// <summary>
/// LA-1 — the <see cref="LiveActivityToken"/> schema contract (ADR-0029 D3): Auditable + tenant-scoped
/// (S8 global query filter, not hand-rolled), mapped to its own table, unique on (UserId, DeviceId,
/// OrderId) so registration upserts, and a nullable OrderId (null = the push-to-start token).
/// </summary>
public sealed class LiveActivityTokenModelMetadataTests
{
    private static IEntityType GetEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(LiveActivityToken));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static bool HasUniqueIndexOn(IEntityType entityType, params string[] columns) =>
        entityType.GetIndexes().Any(ix => ix.IsUnique && ix.Properties.Select(p => p.Name).SequenceEqual(columns));

    private static bool HasIndexOn(IEntityType entityType, params string[] columns) =>
        entityType.GetIndexes().Any(ix => ix.Properties.Select(p => p.Name).SequenceEqual(columns));

    [Fact]
    public void LiveActivityToken_Is_Auditable_And_TenantScoped()
    {
        Assert.True(typeof(Auditable).IsAssignableFrom(typeof(LiveActivityToken)));
        Assert.True(typeof(ITenantEntity).IsAssignableFrom(typeof(LiveActivityToken)));
    }

    [Fact]
    public void LiveActivityToken_Has_Tenant_Global_Query_Filter()
    {
        Assert.NotNull(GetEntityType().GetQueryFilter());
    }

    [Fact]
    public void LiveActivityToken_Is_Mapped_To_Its_Table()
    {
        Assert.Equal("LiveActivityTokens", GetEntityType().GetTableName());
    }

    [Fact]
    public void LiveActivityToken_Is_Unique_On_User_Device_Order()
    {
        Assert.True(HasUniqueIndexOn(GetEntityType(),
            nameof(LiveActivityToken.UserId),
            nameof(LiveActivityToken.DeviceId),
            nameof(LiveActivityToken.OrderId)));
    }

    [Fact]
    public void LiveActivityToken_Has_User_Order_Lookup_Index()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(LiveActivityToken.UserId),
            nameof(LiveActivityToken.OrderId)));
    }

    [Fact]
    public void OrderId_Is_Nullable()
    {
        var property = GetEntityType().FindProperty(nameof(LiveActivityToken.OrderId));
        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }

    [Theory]
    [InlineData(nameof(LiveActivityToken.UserId))]
    [InlineData(nameof(LiveActivityToken.DeviceId))]
    [InlineData(nameof(LiveActivityToken.Token))]
    public void Required_Columns_Are_Not_Nullable(string propertyName)
    {
        var property = GetEntityType().FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
