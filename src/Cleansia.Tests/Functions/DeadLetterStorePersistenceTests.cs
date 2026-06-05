using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Functions;

/// <summary>
/// TC-POISON-0 (durable half — ADR-0002 D3 / T-0120 AC3) — the <see cref="IDeadLetterStore"/>
/// Wave-0 backing writes a real, durable <see cref="DeadLetter"/> row (source queue + raw body) and
/// OWNS ITS OWN COMMIT (the poison consumer has no pipeline/UnitOfWork). Spins a REAL
/// <see cref="CleansiaDbContext"/> over SQLite in-memory so <c>OnModelCreating</c> + the entity config
/// + the tenant query filter actually run.
///
/// Test-first (RED until <c>DeadLetter</c> + its EF config + <c>DeadLetterRepository</c> +
/// <c>DeadLetterStore</c> exist).
/// </summary>
public sealed class DeadLetterStorePersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DeadLetterStorePersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

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

    [Fact]
    public async Task RecordAsync_Persists_A_Durable_DeadLetter_Row_With_Source_Queue_And_Body()
    {
        await using (var seedCtx = NewContext(null))
        {
            await seedCtx.Database.EnsureCreatedAsync();
        }

        const string body = "{\"messageKey\":\"receipt:ORDER-1\",\"payload\":{\"orderId\":\"ORDER-1\"}}";

        // Write through the real store (own commit).
        await using (var writeCtx = NewContext(null))
        {
            var store = new DeadLetterStore(new DeadLetterRepository(writeCtx));
            await store.RecordAsync(QueueNames.GenerateReceipt, body, error: "boom", CancellationToken.None);
        }

        // Read back through a fresh context — the row is durable (committed).
        await using var readCtx = NewContext(null);
        var rows = await readCtx.Set<DeadLetter>().ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(QueueNames.GenerateReceipt, row.SourceQueue);
        Assert.Equal(body, row.RawBody);
        Assert.Equal("boom", row.Error);
    }

    [Fact]
    public async Task RecordAsync_Allows_Null_Tenant_For_Unparseable_Poison_Body()
    {
        await using (var seedCtx = NewContext(null))
        {
            await seedCtx.Database.EnsureCreatedAsync();
        }

        // A malformed body with no derivable tenant — the row must still persist with TenantId == null.
        await using (var writeCtx = NewContext(null))
        {
            var store = new DeadLetterStore(new DeadLetterRepository(writeCtx));
            await store.RecordAsync(QueueNames.GenerateInvoice, "totally-unparseable", error: null, CancellationToken.None);
        }

        await using var readCtx = NewContext(null);
        var row = Assert.Single(await readCtx.Set<DeadLetter>().IgnoreQueryFilters().ToListAsync());
        Assert.Null(row.TenantId);
        Assert.Null(row.Error);
        Assert.Equal("totally-unparseable", row.RawBody);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
