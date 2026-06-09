using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Outbox;

/// <summary>
/// The outbox claim against a REAL Postgres DbContext (Testcontainers). The Postgres claim path
/// (<c>UPDATE ... RETURNING *</c> under <c>FOR UPDATE SKIP LOCKED</c>) is provider-gated and never runs
/// on the SQLite unit tests — so this is the only coverage that exercises the raw, non-composable SQL
/// that EF must run as-is. Covers: a Pending row is claimed and returned; a Dispatched row is never
/// reclaimed; a future-scheduled row is skipped until due; a live lease blocks a second claim; and N
/// concurrent drainers on independent connections never claim the same row (SKIP LOCKED).
/// </summary>
[Collection("PostgresCollection")]
public class OutboxClaimPostgresTests : BaseIntegrationTest
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(120);

    public OutboxClaimPostgresTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
    }

    private static OutboxMessage Pending(string orderId) =>
        OutboxMessage.Create(QueueNames.GenerateReceipt, $"receipt:{orderId}", "{}", null);

    private static Task<IReadOnlyList<OutboxMessage>> Claim(
        OutboxMessageRepository repository, string token, DateTimeOffset now, int batchSize = 100) =>
        repository.ClaimPendingBatchAsync(token, batchSize, now, now - Lease, CancellationToken.None);

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
    }

    private async Task SeedAsync(params OutboxMessage[] rows)
    {
        await using var ctx = NewContext();
        ctx.OutboxMessages.AddRange(rows);
        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_Committed_Pending_Row_Is_Claimed_And_Returned()
    {
        await ResetAsync();
        await SeedAsync(Pending("ORDER-1"));

        await using var ctx = NewContext();
        var claimed = await Claim(new OutboxMessageRepository(ctx), "drainer-1", DateTimeOffset.UtcNow);

        var row = Assert.Single(claimed);
        Assert.Equal("receipt:ORDER-1", row.MessageKey);
        Assert.Equal("drainer-1", row.ClaimedBy);
        Assert.NotNull(row.ClaimedOn);
    }

    [Fact]
    public async Task A_Dispatched_Row_Is_Never_Reclaimed()
    {
        await ResetAsync();
        var dispatched = Pending("ORDER-1");
        dispatched.MarkDispatched(DateTimeOffset.UtcNow);
        await SeedAsync(dispatched);

        await using var ctx = NewContext();
        Assert.Empty(await Claim(new OutboxMessageRepository(ctx), "drainer-1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task A_Row_Scheduled_For_The_Future_Is_Skipped_Until_It_Is_Due()
    {
        await ResetAsync();
        var notYetDue = Pending("ORDER-1");
        notYetDue.Reschedule(DateTimeOffset.UtcNow.AddMinutes(10), "transient");
        await SeedAsync(notYetDue);

        await using var ctx = NewContext();
        var repository = new OutboxMessageRepository(ctx);
        Assert.Empty(await Claim(repository, "drainer-1", DateTimeOffset.UtcNow));
        Assert.Single(await Claim(repository, "drainer-1", DateTimeOffset.UtcNow.AddMinutes(11)));
    }

    [Fact]
    public async Task A_Live_Claim_Is_Not_Returned_By_A_Second_Claim_Until_The_Lease_Expires()
    {
        await ResetAsync();
        await SeedAsync(Pending("ORDER-1"));
        var claimedAt = DateTimeOffset.UtcNow;

        await using (var firstCtx = NewContext())
        {
            var repository = new OutboxMessageRepository(firstCtx);
            Assert.Single(await Claim(repository, "drainer-1", claimedAt));
            await repository.CommitAsync(CancellationToken.None);
        }

        await using (var secondCtx = NewContext())
        {
            var whileLeaseLive = await Claim(new OutboxMessageRepository(secondCtx), "drainer-2", claimedAt.AddSeconds(30));
            Assert.Empty(whileLeaseLive);
        }

        await using var thirdCtx = NewContext();
        var afterLeaseExpiry = await Claim(
            new OutboxMessageRepository(thirdCtx), "drainer-3", claimedAt + Lease + TimeSpan.FromSeconds(1));
        Assert.Single(afterLeaseExpiry);
    }

    [Fact]
    public async Task Concurrent_Drainers_Never_Claim_The_Same_Row()
    {
        await ResetAsync();

        const int rowCount = 40;
        const int drainers = 8;
        await SeedAsync(Enumerable.Range(0, rowCount).Select(i => Pending($"ORDER-{i}")).ToArray());

        var now = DateTimeOffset.UtcNow;
        var tasks = Enumerable.Range(0, drainers).Select(async d =>
        {
            await using var ctx = NewContext();
            var repository = new OutboxMessageRepository(ctx);
            var claimed = await Claim(repository, $"drainer-{d}", now, batchSize: 10);
            await repository.CommitAsync(CancellationToken.None);
            return claimed.Select(m => m.Id).ToArray();
        }).ToArray();

        var batches = await Task.WhenAll(tasks);

        var allClaimed = batches.SelectMany(b => b).ToArray();
        // No row claimed twice (SKIP LOCKED gives each drainer a disjoint set), and total never exceeds
        // what exists.
        Assert.Equal(allClaimed.Length, allClaimed.Distinct().Count());
        Assert.True(allClaimed.Length <= rowCount);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
