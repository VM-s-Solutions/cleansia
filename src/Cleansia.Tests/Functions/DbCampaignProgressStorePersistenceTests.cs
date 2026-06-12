using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0010 — the durable backing for <see cref="ICampaignProgressStore"/>. Replaces the process-local
/// cursor so a sitewide-promo fan-out resumes past the last processed recipient after a worker restart /
/// scale-out instead of re-costing the whole opted-in base. <see cref="DbCampaignProgressStore"/> upserts
/// a <see cref="Core.Domain.Messaging.CampaignProgress"/> row in its OWN commit (the fan-out consumer has
/// no MediatR UnitOfWork) — a Bucket-C cost layer, not an at-most-once effect control.
///
/// Spins a REAL <see cref="CleansiaDbContext"/> over SQLite so the entity config + the one-row-per-
/// campaign unique index run. Test-first (RED until the entity + EF config + repository + store exist).
/// </summary>
public sealed class DbCampaignProgressStorePersistenceTests : IDisposable
{
    private const string CampaignId = "promo::durable-cursor";
    private readonly SqliteConnection _connection;

    public DbCampaignProgressStorePersistenceTests()
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

    private static DbCampaignProgressStore Store(CleansiaDbContext ctx) =>
        new(new CampaignProgressRepository(ctx));

    [Fact]
    public async Task Get_For_An_Unknown_Campaign_Returns_A_Null_Cursor_And_Not_Complete()
    {
        await EnsureSchemaAsync();

        await using var ctx = NewContext();
        var progress = await Store(ctx).GetAsync(CampaignId, CancellationToken.None);

        Assert.Null(progress.LastProcessedUserId);
        Assert.False(progress.IsComplete);
    }

    [Fact]
    public async Task Advance_Then_Get_Round_Trips_The_Cursor_Durably()
    {
        await EnsureSchemaAsync();

        // Advance through one store/context, read back through a fresh one (own-commit → durable).
        await using (var writeCtx = NewContext())
        {
            await Store(writeCtx).AdvanceAsync(CampaignId, "USER-7", CancellationToken.None);
        }

        await using var readCtx = NewContext();
        var progress = await Store(readCtx).GetAsync(CampaignId, CancellationToken.None);

        Assert.Equal("USER-7", progress.LastProcessedUserId);
        Assert.False(progress.IsComplete);
    }

    [Fact]
    public async Task Repeated_Advance_Upserts_The_Same_Single_Row_And_Moves_The_Cursor()
    {
        await EnsureSchemaAsync();

        await using (var ctx1 = NewContext())
        {
            await Store(ctx1).AdvanceAsync(CampaignId, "USER-1", CancellationToken.None);
        }
        await using (var ctx2 = NewContext())
        {
            await Store(ctx2).AdvanceAsync(CampaignId, "USER-5", CancellationToken.None);
        }

        await using var readCtx = NewContext();
        var progress = await Store(readCtx).GetAsync(CampaignId, CancellationToken.None);

        // Cursor moved forward; the unique index kept it to ONE row (find-or-insert-then-update upsert).
        Assert.Equal("USER-5", progress.LastProcessedUserId);
        Assert.Single(await readCtx.Set<Core.Domain.Messaging.CampaignProgress>().ToListAsync());
    }

    [Fact]
    public async Task MarkComplete_Then_Get_Reports_Complete_Durably()
    {
        await EnsureSchemaAsync();

        await using (var advanceCtx = NewContext())
        {
            await Store(advanceCtx).AdvanceAsync(CampaignId, "USER-3", CancellationToken.None);
        }
        await using (var completeCtx = NewContext())
        {
            await Store(completeCtx).MarkCompleteAsync(CampaignId, CancellationToken.None);
        }

        await using var readCtx = NewContext();
        var progress = await Store(readCtx).GetAsync(CampaignId, CancellationToken.None);

        Assert.True(progress.IsComplete);
        Assert.Equal("USER-3", progress.LastProcessedUserId); // completion preserves the cursor.
    }

    [Fact]
    public async Task MarkComplete_On_An_Unknown_Campaign_Inserts_A_Completed_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await Store(ctx).MarkCompleteAsync(CampaignId, CancellationToken.None);
        }

        await using var readCtx = NewContext();
        var progress = await Store(readCtx).GetAsync(CampaignId, CancellationToken.None);

        Assert.True(progress.IsComplete);
        Assert.Null(progress.LastProcessedUserId);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
