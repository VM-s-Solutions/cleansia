using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Receipts;

/// <summary>
/// FISCAL-SEQ suite for the gapless-monotonic-atomic allocator over a REAL Postgres DbContext
/// (Testcontainers) — SQLite cannot model the row-lock / <c>RETURNING</c> concurrency the contract
/// rests on. Covers: N concurrent allocations on independent connections yield N distinct contiguous
/// numbers; a rolled-back claim returns its number to the pool without shifting the next allocation;
/// year-reset vs non-year-reset issuer-scope semantics.
///
/// <para>Runs against the applied EF migration history, so it goes green once the owner regenerates
/// the Initial migration to include the FiscalCounters table (the schema delta is an owner-only
/// ef-migration step).</para>
/// </summary>
[Collection("PostgresCollection")]
public class FiscalCounterAllocatorTests : BaseIntegrationTest
{
    private const string Scope = "cz-eet2";

    public FiscalCounterAllocatorTests(PostgresContainerFixture fixture) : base(fixture)
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

    private FiscalCounterRepository NewRepository(CleansiaDbContext context) =>
        new(context, new FixedTenantProvider(tenantId: null), new TestUserSessionProvider("system", "system@cleansia.test"));

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

    [Fact]
    public async Task N_Concurrent_Allocations_Yield_N_Distinct_Contiguous_Numbers()
    {
        await ResetAsync();

        const int concurrency = 25;

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await using var context = NewContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var repository = NewRepository(context);
            var value = await repository.AllocateNextAsync(2026, Scope, CancellationToken.None);
            await transaction.CommitAsync();
            return value;
        }).ToArray();

        var allocated = await Task.WhenAll(tasks);

        Assert.Equal(concurrency, allocated.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, concurrency).Select(i => (long)i), allocated.OrderBy(v => v));
    }

    [Fact]
    public async Task RolledBack_Claim_Does_Not_Shift_The_Next_Allocation()
    {
        await ResetAsync();

        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var first = await NewRepository(context).AllocateNextAsync(2026, Scope, CancellationToken.None);
            Assert.Equal(1, first);
            await transaction.CommitAsync();
        }

        // A claim that allocates a number then rolls back must return that number to the pool: the
        // counter is bound to the transaction, so the next committed allocation reuses it.
        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var rolledBack = await NewRepository(context).AllocateNextAsync(2026, Scope, CancellationToken.None);
            Assert.Equal(2, rolledBack);
            await transaction.RollbackAsync();
        }

        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var next = await NewRepository(context).AllocateNextAsync(2026, Scope, CancellationToken.None);
            Assert.Equal(2, next);
            await transaction.CommitAsync();
        }
    }

    [Fact]
    public async Task Year_Reset_Scope_Restarts_At_Year_Boundary()
    {
        await ResetAsync();

        var (year2026, scope) = Cleansia.Core.Fiscal.Abstractions.FiscalSequenceScope.Resolve("cz-eet2", 2026);
        var (year2027, _) = Cleansia.Core.Fiscal.Abstractions.FiscalSequenceScope.Resolve("cz-eet2", 2027);

        long firstOf2026;
        long firstOf2027;

        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var repository = NewRepository(context);
            firstOf2026 = await repository.AllocateNextAsync(year2026, scope, CancellationToken.None);
            await repository.AllocateNextAsync(year2026, scope, CancellationToken.None);
            await transaction.CommitAsync();
        }

        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            firstOf2027 = await NewRepository(context).AllocateNextAsync(year2027, scope, CancellationToken.None);
            await transaction.CommitAsync();
        }

        Assert.Equal(1, firstOf2026);
        Assert.Equal(1, firstOf2027);
    }

    [Fact]
    public async Task Non_Year_Reset_Scope_Continues_Across_Year_Boundary()
    {
        await ResetAsync();

        var (year, scope) = Cleansia.Core.Fiscal.Abstractions.FiscalSequenceScope.Resolve("de-tss-fiskaly", 2026);
        var (yearNextCalendar, scopeNextCalendar) =
            Cleansia.Core.Fiscal.Abstractions.FiscalSequenceScope.Resolve("de-tss-fiskaly", 2027);

        Assert.Equal(year, yearNextCalendar);
        Assert.Equal(scope, scopeNextCalendar);

        long beforeBoundary;
        long afterBoundary;

        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var repository = NewRepository(context);
            await repository.AllocateNextAsync(year, scope, CancellationToken.None);
            beforeBoundary = await repository.AllocateNextAsync(year, scope, CancellationToken.None);
            await transaction.CommitAsync();
        }

        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            afterBoundary = await NewRepository(context)
                .AllocateNextAsync(yearNextCalendar, scopeNextCalendar, CancellationToken.None);
            await transaction.CommitAsync();
        }

        Assert.Equal(2, beforeBoundary);
        Assert.Equal(3, afterBoundary);
    }

    [Fact]
    public async Task Distinct_Issuer_Scopes_Do_Not_Share_A_Sequence()
    {
        await ResetAsync();

        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var repository = NewRepository(context);

        var firstA = await repository.AllocateNextAsync(2026, "cz-eet2", CancellationToken.None);
        var firstB = await repository.AllocateNextAsync(2026, "sk-ekasa", CancellationToken.None);
        var secondA = await repository.AllocateNextAsync(2026, "cz-eet2", CancellationToken.None);

        await transaction.CommitAsync();

        Assert.Equal(1, firstA);
        Assert.Equal(1, firstB);
        Assert.Equal(2, secondA);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}

