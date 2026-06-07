using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Outbox;

public sealed class OutboxMessageMappingTests
{
    private static IEntityType GetOutboxEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(OutboxMessage));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static bool IsCompositeIndexOn(IIndex index, params string[] propertyNames) =>
        index.Properties.Count == propertyNames.Length
        && index.Properties.Select(p => p.Name).SequenceEqual(propertyNames);

    [Fact]
    public void OutboxMessage_IsMappedToTable()
    {
        var outbox = GetOutboxEntityType();

        Assert.Equal("OutboxMessages", outbox.GetTableName());
    }

    [Fact]
    public void QueueNameMessageKey_HasExactlyOneUniqueIndex()
    {
        var outbox = GetOutboxEntityType();

        var keyIndexes = outbox.GetIndexes()
            .Where(ix => IsCompositeIndexOn(ix, nameof(OutboxMessage.QueueName), nameof(OutboxMessage.MessageKey)))
            .ToList();

        Assert.Single(keyIndexes);
        Assert.True(
            keyIndexes[0].IsUnique,
            "(QueueName, MessageKey) must be the UNIQUE index: it is the in-request idempotency a "
            + "double-enqueue collapses onto, so the durable backing matches the in-memory buffer.");
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
