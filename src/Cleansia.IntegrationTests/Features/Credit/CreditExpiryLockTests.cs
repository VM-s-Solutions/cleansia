using System.Data.Common;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Credit;
using Cleansia.Core.AppServices.Features.Credit.Admin;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Credit;

/// <summary>
/// The two tracked drains — the nightly expiry sweep and the admin discharge — write an absolute
/// balance, so each must read it under the owner lock. Otherwise a return or an erasure committed on
/// another connection in between is overwritten, and Balance == SUM(ledger) breaks.
/// </summary>
[Collection("PostgresCollection")]
public class CreditExpiryLockTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string ActorId = "01ADMINCREDITLOCK000000001";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_Sweep_Rereads_A_Balance_Moved_After_Its_Batch_Was_Loaded(bool erasedMeanwhile)
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedAsync(100m, expired: true);
        var beforeLock = new BeforeFirstOwnerLock(async () =>
        {
            if (erasedMeanwhile)
                await CommitErasureAsync(userId);
            else
                Assert.True(await ReturnAsync(userId, currencyId, 25m, "credit-return:during-sweep"));
        });

        await using var sweeping = NewContext(beforeLock);
        var result = await new ExpireStaleCredit.Handler(
                new CreditAccountRepository(sweeping), new FixedTenantProvider(null), sweeping,
                NullLogger<ExpireStaleCredit.Handler>.Instance)
            .Handle(new ExpireStaleCredit.Command(), CancellationToken.None);

        Assert.True(beforeLock.Ran);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.AccountsExpired);
        var account = await AccountAsync(userId);
        Assert.Equal(erasedMeanwhile ? 0m : 125m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.DoesNotContain(account.Transactions, t => t.IdempotencyKey.StartsWith("credit-expired:"));
    }

    [Fact]
    public async Task An_Admin_Discharge_Waits_For_A_Concurrent_Return_And_Takes_It_Too()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedAsync(100m, expired: false);

        await using var returning = NewContext();
        await using var returnTransaction = await returning.Database.BeginTransactionAsync();
        Assert.True(await new CreditAccountRepository(returning).TryReturnAsync(
            userId, currencyId, 25m, "credit-return:during-discharge", ActorId, CancellationToken.None));

        await using var discharging = NewContext();
        await discharging.Database.OpenConnectionAsync();
        var handler = new ExpireCustomerCredit.Handler(
            new CreditAccountRepository(discharging), new CurrencyRepository(discharging),
            new TestUserSessionProvider(ActorId, "admin@cleansia.test"), new AuditContext());
        var discharge = DischargeAsync();
        await WaitUntilBlocked(((NpgsqlConnection)discharging.Database.GetDbConnection()).ProcessID, discharge);
        Assert.False(discharge.IsCompleted, "the discharge must wait for the return's lock");

        await returnTransaction.CommitAsync();
        var result = await discharge.WaitAsync(Timeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(125m, result.Value!.AmountExpired);
        var account = await AccountAsync(userId);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(0m, account.Transactions.Sum(t => t.Amount));

        async Task<BusinessResult<ExpireCustomerCredit.Response>> DischargeAsync()
        {
            var discharged = await handler.Handle(
                new ExpireCustomerCredit.Command(userId, currencyId, "Customer asked to discharge", "discharge-1"),
                CancellationToken.None);
            await discharging.CommitAsync(CancellationToken.None);
            return discharged;
        }
    }

    private CleansiaDbContext NewContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString());
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new CleansiaDbContext(options.Options,
            new TestUserSessionProvider("system", "system@cleansia.test"), new FixedTenantProvider(TestTenants.Default));
    }

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
        await SeedTenantRegistryAsync(conn);
    }

    private async Task<(string UserId, string CurrencyId)> SeedAsync(decimal balance, bool expired)
    {
        await using var ctx = NewContext();
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.IsActive = true;
        var user = User.CreateWithPassword("credit-lock@cleansia.test", "Seed-Password-123", "Credit", "Lock");
        ctx.Languages.Add(Language.Create("en", "English"));
        ctx.Currencies.Add(currency);
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);

        await using var seed = NewContext();
        var account = await new CreditAccountRepository(seed).EnsureForUserAsync(user.Id, currency.Id, CancellationToken.None);
        account!.Issue(balance, CreditTransactionReason.Goodwill, "seed-grant", ActorId);
        await seed.CommitAsync(CancellationToken.None);

        if (expired)
        {
            await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
            await conn.OpenAsync();
            await using var command = new NpgsqlCommand(
                """UPDATE "CreditAccounts" SET "ExpiresOn" = NOW() - INTERVAL '1 day' WHERE "UserId" = @id""", conn);
            command.Parameters.AddWithValue("id", user.Id);
            await command.ExecuteNonQueryAsync();
        }

        return (user.Id, currency.Id);
    }

    /// <summary>The state a completed erasure commits: an erased owner and a forfeited balance.</summary>
    private async Task CommitErasureAsync(string userId)
    {
        await using var erase = NewContext();
        var user = await erase.Users.SingleAsync(u => u.Id == userId);
        user.Anonymize().Deactivated("GDPR_DELETION", DateTimeOffset.UtcNow);
        var account = await erase.CreditAccounts.SingleAsync(a => a.UserId == userId);
        account.RecordExpiry(account.Drain(ActorId, DateTimeOffset.UtcNow),
            $"account-deletion:{account.Id}", ActorId, "Account deletion");
        await erase.CommitAsync(CancellationToken.None);
    }

    private async Task<bool> ReturnAsync(string userId, string currencyId, decimal amount, string key)
    {
        await using var ctx = NewContext();
        return await new CreditAccountRepository(ctx).TryReturnAsync(
            userId, currencyId, amount, key, ActorId, CancellationToken.None);
    }

    private async Task<CreditAccount> AccountAsync(string userId)
    {
        await using var ctx = NewContext();
        return await ctx.CreditAccounts.AsNoTracking().Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId);
    }

    private async Task WaitUntilBlocked(int backendPid, Task work)
    {
        using var deadline = new CancellationTokenSource(Timeout);
        await using var observer = new NpgsqlConnection(Fixture.GetConnectionString());
        await observer.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid)) > 0", observer);
        command.Parameters.AddWithValue("pid", backendPid);
        while (await command.ExecuteScalarAsync(deadline.Token) is not true && !work.IsCompleted)
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
    }

    /// <summary>Commits another connection's write just before the sweep takes its first owner lock.</summary>
    private sealed class BeforeFirstOwnerLock(Func<Task> write) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Ran && command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
            {
                Ran = true;
                await write();
            }
            return result;
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? currentTenantId = tenantId;
        public string? GetCurrentTenantId() => currentTenantId;
        public void SetTenantOverride(string tenantId) => currentTenantId = tenantId;
        public void ClearTenantOverride() => currentTenantId = null;
    }
}
