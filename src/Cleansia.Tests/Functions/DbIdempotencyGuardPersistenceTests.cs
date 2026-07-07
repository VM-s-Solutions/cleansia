using Cleansia.Core.Domain.Messaging;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0010 — the durable backing for <see cref="IIdempotencyGuard"/>. Owner decision: the production
/// guard MUST survive a worker restart / scale-out, because T-0182 makes the push guard the load-bearing
/// at-most-once control — a process-local <c>ConcurrentDictionary</c> means duplicate pushes after a
/// restart. <see cref="DbIdempotencyGuard"/> claims a unique <see cref="ProcessedMessage"/> row in its
/// OWN commit (mirroring the <c>ProcessedStripeEvent</c> pattern + the <c>DeadLetterStore</c> own-commit
/// discipline).
///
/// Spins a REAL <see cref="CleansiaDbContext"/> over SQLite in-memory so <c>OnModelCreating</c> + the
/// entity config + the REAL unique index actually run — the 23505/parallel-claim collapse is proven
/// against the real model, not a mocked exception.
///
/// Test-first (RED until ProcessedMessage + its EF config + repository + DbIdempotencyGuard exist).
/// </summary>
public sealed class DbIdempotencyGuardPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbIdempotencyGuardPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static DbIdempotencyGuard Guard(CleansiaDbContext ctx) =>
        new(new ProcessedMessageRepository(ctx));

    [Fact]
    public async Task First_Claim_Returns_False_Then_Second_Claim_For_Same_Key_Returns_True()
    {
        await EnsureSchemaAsync();
        const string key = "push:USER-1:order.confirmed:ORDER-1";

        await using var ctx1 = NewContext();
        var first = await Guard(ctx1).AlreadyProcessedAsync(key, CancellationToken.None);

        await using var ctx2 = NewContext();
        var second = await Guard(ctx2).AlreadyProcessedAsync(key, CancellationToken.None);

        Assert.False(first);  // Won the claim → proceed with the effect.
        Assert.True(second);  // Already claimed → ack, do not re-send.
    }

    [Fact]
    public async Task A_Claim_Survives_Across_Context_Instances_Simulating_A_Worker_Restart()
    {
        await EnsureSchemaAsync();
        const string key = "email:ConfirmationEmail:USER-2:abc123";

        // First worker instance claims the key, then "dies" (its context is disposed).
        await using (var workerA = NewContext())
        {
            Assert.False(await Guard(workerA).AlreadyProcessedAsync(key, CancellationToken.None));
        }

        // A fresh worker instance (post-restart / a scaled-out peer) sees the durable claim row and
        // short-circuits — the in-memory guard could NOT do this.
        await using var workerB = NewContext();
        Assert.True(await Guard(workerB).AlreadyProcessedAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task The_Won_Claim_Commits_A_Durable_ProcessedMessage_Row()
    {
        await EnsureSchemaAsync();
        const string key = "push:USER-3:order.cancelled:ORDER-9";

        await using (var ctx = NewContext())
        {
            await Guard(ctx).AlreadyProcessedAsync(key, CancellationToken.None);
        }

        // Read back through a fresh context — the claim is durable (own-commit).
        await using var readCtx = NewContext();
        var row = Assert.Single(await readCtx.Set<ProcessedMessage>().ToListAsync());
        Assert.Equal(key, row.MessageKey);
    }

    [Fact]
    public async Task Parallel_Claim_Loser_Catches_The_Unique_Violation_And_Resolves_To_Already_Claimed()
    {
        await EnsureSchemaAsync();
        const string key = "push:USER-4:order.confirmed:ORDER-RACE";

        // Winner stages + commits its claim row directly, populating the REAL unique index.
        await using (var contextWinner = NewContext())
        {
            var winnerRepo = new ProcessedMessageRepository(contextWinner);
            winnerRepo.Add(ProcessedMessage.Create(key));
            await winnerRepo.CommitAsync(CancellationToken.None);
        }

        // The loser claims the SAME key through the guard on a fresh context — its Add+CommitAsync
        // collides on the index (SQLite SQLITE_CONSTRAINT). The guard must convert that into
        // already-claimed (true), NOT let the DbUpdateException escape (which would poison-loop).
        await using var contextLoser = NewContext();
        var loserResult = await Guard(contextLoser).AlreadyProcessedAsync(key, CancellationToken.None);

        Assert.True(loserResult); // Collapsed to already-claimed, no throw.

        // Exactly one durable row — the winner's.
        await using var readCtx = NewContext();
        Assert.Single(await readCtx.Set<ProcessedMessage>().ToListAsync());
    }

    [Fact]
    public async Task A_Non_Unique_Db_Fault_Is_Not_Swallowed_And_Re_Throws()
    {
        await EnsureSchemaAsync();

        // A genuine infra fault on commit (NOT a unique-violation) must propagate so the queue retries —
        // the guard only swallows 23505 / SQLITE_CONSTRAINT, everything else bubbles.
        var guard = new DbIdempotencyGuard(InfraFaultRepository());

        await Assert.ThrowsAsync<DbUpdateException>(
            () => guard.AlreadyProcessedAsync("push:USER-5:x:y", CancellationToken.None));
    }

    [Fact]
    public async Task HasProcessed_Is_A_Non_Claiming_Read()
    {
        await EnsureSchemaAsync();
        const string key = "email:ResetPassword:USER-6:hash1";

        await using (var readCtx = NewContext())
        {
            Assert.False(await Guard(readCtx).HasProcessedAsync(key, CancellationToken.None));
        }

        // The read wrote nothing — a fresh guard still WINS the claim for the same key.
        await using var claimCtx = NewContext();
        Assert.False(await Guard(claimCtx).AlreadyProcessedAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Mark_Commits_A_Durable_Claim_That_HasProcessed_Reads_Back()
    {
        await EnsureSchemaAsync();
        const string key = "email:ConfirmationEmail:USER-7:hash2";

        await using (var markCtx = NewContext())
        {
            await Guard(markCtx).MarkProcessedAsync(key, CancellationToken.None);
        }

        await using var readCtx = NewContext();
        Assert.True(await Guard(readCtx).HasProcessedAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Double_Mark_And_Mark_After_A_Concurrent_Claim_Are_Safe_NoOps()
    {
        await EnsureSchemaAsync();
        const string key = "email:ConfirmationEmail:USER-8:hash3";

        // A concurrent consumer already claimed the key …
        await using (var winner = NewContext())
        {
            Assert.False(await Guard(winner).AlreadyProcessedAsync(key, CancellationToken.None));
        }

        // … marking after it, twice, must neither throw nor duplicate the row.
        await using (var markCtx = NewContext())
        {
            await Guard(markCtx).MarkProcessedAsync(key, CancellationToken.None);
        }
        await using (var markAgainCtx = NewContext())
        {
            await Guard(markAgainCtx).MarkProcessedAsync(key, CancellationToken.None);
        }

        await using var readCtx = NewContext();
        Assert.Single(await readCtx.Set<ProcessedMessage>().Where(m => m.MessageKey == key).ToListAsync());
    }

    [Fact]
    public async Task Mark_Swallows_A_Parallel_Claim_Unique_Violation_But_Re_Throws_Infra_Faults()
    {
        // The two-parallel-consumers race: both miss the existence check, the loser's insert hits the
        // unique index — for Mark that is success (the row exists), so no throw.
        var raceLoser = new DbIdempotencyGuard(new ThrowingRepository(
            new DbUpdateException("duplicate key", new SqliteException("UNIQUE constraint failed", 19))));
        await raceLoser.MarkProcessedAsync("email:x:y:z", CancellationToken.None);

        // A genuine infra fault on the claim write must still propagate for the caller to classify.
        var infraFault = new DbIdempotencyGuard(InfraFaultRepository());
        await Assert.ThrowsAsync<DbUpdateException>(
            () => infraFault.MarkProcessedAsync("email:x:y:z", CancellationToken.None));
    }

    private static ThrowingRepository InfraFaultRepository() =>
        new(new DbUpdateException("simulated infra fault", new TimeoutException("connection reset")));

    // A repository whose claim commit throws the given DbUpdateException — proves the guard re-throws
    // genuine infra faults instead of masking them, and collapses unique-violations to already-claimed.
    private sealed class ThrowingRepository(DbUpdateException exception) : IProcessedMessageRepository
    {
        public Task CommitAsync(CancellationToken cancellationToken) => throw exception;

        public void Add(ProcessedMessage entity) { }
        // Not-yet-claimed, so the guard proceeds to the throwing CommitAsync (the point of this test).
        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsAsync(string id, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistWithIdsAsync(IEnumerable<string> ids, CancellationToken ct) => Task.FromResult(false);
        public Task<ProcessedMessage?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<ProcessedMessage?>(null);
        public IQueryable<ProcessedMessage> GetByIds(IEnumerable<string> ids) => Array.Empty<ProcessedMessage>().AsQueryable();
        public IQueryable<ProcessedMessage> GetPaged(int offset, int limit) => Array.Empty<ProcessedMessage>().AsQueryable();
        public IQueryable<ProcessedMessage> GetPaged(int offset, int limit, System.Linq.Expressions.Expression<Func<ProcessedMessage, bool>> filter) => Array.Empty<ProcessedMessage>().AsQueryable();
        public IQueryable<ProcessedMessage> GetPagedSort<TSort>(int offset, int limit, System.Linq.Expressions.Expression<Func<ProcessedMessage, bool>> filter, Core.Domain.Sorting.Common.SortDefinition sort) where TSort : Core.Domain.Sorting.Common.BaseSort<ProcessedMessage> => Array.Empty<ProcessedMessage>().AsQueryable();
        public IQueryable<ProcessedMessage> GetPagedSort<TSort>(int offset, int limit, System.Linq.Expressions.Expression<Func<ProcessedMessage, bool>>? filter, IEnumerable<Core.Domain.Sorting.Common.SortDefinition> sort) where TSort : Core.Domain.Sorting.Common.BaseSort<ProcessedMessage> => Array.Empty<ProcessedMessage>().AsQueryable();
        public Task<int> GetCountAsync(CancellationToken ct) => Task.FromResult(0);
        public Task<int> GetCountAsync(System.Linq.Expressions.Expression<Func<ProcessedMessage, bool>>? filter, CancellationToken ct) => Task.FromResult(0);
        public IQueryable<ProcessedMessage> GetFiltered(System.Linq.Expressions.Expression<Func<ProcessedMessage, bool>> filter) => Array.Empty<ProcessedMessage>().AsQueryable();
        public IQueryable<ProcessedMessage> GetAll() => Array.Empty<ProcessedMessage>().AsQueryable();
        public void AddRange(IEnumerable<ProcessedMessage> entities) { }
        public void Remove(ProcessedMessage entity) { }
        public void RemoveRange(IEnumerable<ProcessedMessage> entities) { }
        public void Deactivate(ProcessedMessage entity) { }
        public void DeactivateRange(IEnumerable<ProcessedMessage> entities) { }
        public IQueryable<ProcessedMessage> GetQueryable() => Array.Empty<ProcessedMessage>().AsQueryable();
        public IQueryable<ProcessedMessage> GetQueryableIgnoringTenant() => Array.Empty<ProcessedMessage>().AsQueryable();
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) => throw new NotSupportedException();
        public void Rollback() { }
        public void Dispose() { }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
