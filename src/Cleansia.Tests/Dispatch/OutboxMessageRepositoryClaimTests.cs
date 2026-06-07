using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// TC-OUTBOX-DRAIN-0 (claim half) — the repository's claim against a real
/// <see cref="CleansiaDbContext"/>: a committed Pending row is claimed and returned; a Dispatched row
/// is never re-claimed (so a crash-and-restart does not re-send a delivered message); a row whose
/// NextAttemptAt is in the future is skipped until it is due (the retry backoff); and once a claim is
/// committed a second claim does not return the same row (it is now leased).
/// </summary>
public sealed class OutboxMessageRepositoryClaimTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public OutboxMessageRepositoryClaimTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));
    }

    private async Task SeedAsync(params OutboxMessage[] rows)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
        ctx.OutboxMessages.AddRange(rows);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(120);

    private static OutboxMessage Pending(string orderId) =>
        OutboxMessage.Create(QueueNames.GenerateReceipt, $"receipt:{orderId}", "{}", null);

    private static Task<IReadOnlyList<OutboxMessage>> Claim(
        OutboxMessageRepository repository, string token, DateTimeOffset now) =>
        repository.ClaimPendingBatchAsync(token, 100, now, now - Lease, CancellationToken.None);

    [Fact]
    public async Task A_Committed_Pending_Row_Is_Claimed_On_The_Next_Drain()
    {
        await SeedAsync(Pending("ORDER-1"));

        await using var ctx = NewContext();
        var repository = new OutboxMessageRepository(ctx);
        var claimed = await Claim(repository, "drainer-1", DateTimeOffset.UtcNow);

        var row = Assert.Single(claimed);
        Assert.Equal("receipt:ORDER-1", row.MessageKey);
        Assert.Equal("drainer-1", row.ClaimedBy);
        Assert.NotNull(row.ClaimedOn);
    }

    [Fact]
    public async Task A_Dispatched_Row_Is_Never_Reclaimed()
    {
        var dispatched = Pending("ORDER-1");
        dispatched.MarkDispatched(DateTimeOffset.UtcNow);
        await SeedAsync(dispatched);

        await using var ctx = NewContext();
        var repository = new OutboxMessageRepository(ctx);
        var claimed = await Claim(repository, "drainer-1", DateTimeOffset.UtcNow);

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task A_Row_Scheduled_For_The_Future_Is_Skipped_Until_It_Is_Due()
    {
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
        await SeedAsync(Pending("ORDER-1"));
        var claimedAt = DateTimeOffset.UtcNow;

        await using (var firstCtx = NewContext())
        {
            var repository = new OutboxMessageRepository(firstCtx);
            var first = await Claim(repository, "drainer-1", claimedAt);
            Assert.Single(first);
            await repository.CommitAsync(CancellationToken.None);
        }

        await using (var secondCtx = NewContext())
        {
            var secondRepository = new OutboxMessageRepository(secondCtx);
            var whileLeaseLive = await Claim(secondRepository, "drainer-2", claimedAt.AddSeconds(30));
            Assert.Empty(whileLeaseLive);
        }

        // Once the lease window has elapsed (a crashed drainer that never sent), the row is reclaimable.
        await using var thirdCtx = NewContext();
        var thirdRepository = new OutboxMessageRepository(thirdCtx);
        var afterLeaseExpiry = await Claim(thirdRepository, "drainer-3", claimedAt + Lease + TimeSpan.FromSeconds(1));
        Assert.Single(afterLeaseExpiry);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
