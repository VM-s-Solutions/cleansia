using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// Which earlier dispute blocks a new one, against a real <see cref="CleansiaDbContext"/>.
///
/// <para><b>The defect.</b> The filter was <c>Status != Closed</c>. <c>Resolved</c> has no outgoing
/// transitions in <c>Dispute.AllowedTransitions</c>, and the only <c>Close()</c> callers go through
/// <c>CanTransitionTo</c> — so a resolved dispute could never become closed, and its order was locked
/// out of disputes forever with no admin escape hatch.</para>
///
/// <para>Order-level that reads as a one-shot policy. It becomes a trap the moment a customer is
/// invited to itemise what went wrong, because itemising implies you can come back for the rest.
/// Owner ruling 2026-09-05: resolve it, close it, and a later problem may be raised as a new
/// dispute.</para>
///
/// <para>Exercised through the real DbSet rather than a mock, because the whole change IS the query.</para>
/// </summary>
public sealed class DisputeRepositoryOpenDisputeTests : IDisposable
{
    private const string OrderId = "order-1";
    private readonly SqliteConnection _connection;

    public DisputeRepositoryOpenDisputeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Dispute has required FKs to Order and User. This test is about ONE WHERE clause, so it
        // seeds a dispute and nothing else rather than standing up an order graph that would tell us
        // nothing about the filter. EF's SQLite provider enables FK enforcement; switch it back off.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
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

    private async Task SeedAsync(DisputeStatus status)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var dispute = new Dispute(OrderId, "user-1", DisputeReason.QualityIssue, "Not done.", "user-1");
        if (status == DisputeStatus.Resolved)
        {
            // Resolve owns this state exclusively — it is not reachable through UpdateStatus, which
            // is the whole reason a resolved dispute could never be closed afterwards.
            dispute.Resolve("admin-1", refundAmount: null, resolutionNotes: "Settled.");
        }
        else if (status != DisputeStatus.Pending)
        {
            Assert.True(dispute.UpdateStatus(status, "admin-1"), $"Cannot reach {status} from Pending.");
        }

        ctx.Set<Dispute>().Add(dispute);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<Dispute?> QueryAsync()
    {
        await using var ctx = NewContext();
        return await new DisputeRepository(ctx).GetOpenDisputeForOrderAsync(OrderId, CancellationToken.None);
    }

    [Theory]
    [InlineData(DisputeStatus.Pending)]
    [InlineData(DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.WaitingForResponse)]
    [InlineData(DisputeStatus.Escalated)]
    public async Task AnUnfinishedDispute_StillBlocksASecondOne(DisputeStatus status)
    {
        await SeedAsync(status);

        Assert.NotNull(await QueryAsync());
    }

    [Theory]
    [InlineData(DisputeStatus.Resolved)]
    [InlineData(DisputeStatus.Closed)]
    public async Task AFinishedDispute_NoLongerBlocksANewOne(DisputeStatus status)
    {
        // Resolved is the one that was broken: terminal, unreachable from Closed, and it locked the
        // order permanently.
        await SeedAsync(status);

        Assert.Null(await QueryAsync());
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
