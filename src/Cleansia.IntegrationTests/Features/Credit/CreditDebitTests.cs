using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Credit;

/// <summary>
/// Spending customer credit, against REAL Postgres — the only place it can be tested.
///
/// <para>The debit is a single CTE: a conditional <c>UPDATE … WHERE "Balance" &gt;= amount</c> whose
/// <c>RETURNING</c> feeds the ledger <c>INSERT</c>. SQLite cannot run it, and the semantics that
/// matter here — row locking, and one statement being all-or-nothing — are the database's.</para>
///
/// <para>Two properties, and the design exists for both. A balance is an AGGREGATE, so two concurrent
/// spends share no key and no unique index can arbitrate them; the conditional UPDATE is the arbiter.
/// And the ledger row is written by the SAME statement, so <c>Balance == SUM(Transactions.Amount)</c>
/// cannot drift — an earlier draft left the caller to record the movement, which meant a caller whose
/// own transaction rolled back would have taken the funds and written nothing.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CreditDebitTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string ActorId = "01ADMINCREDIT0000000000001";

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

    /// <summary>
    /// A real customer with an account holding <paramref name="balance"/>, and the ledger row that
    /// put it there. CreditAccount carries FKs to Users and Currencies, so both are seeded properly
    /// rather than worked around.
    /// </summary>
    private async Task<string> SeedAccountAsync(decimal balance)
    {
        await using var ctx = NewContext();

        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.IsActive = true;
        var user = User.CreateWithPassword(
            "credit-tests@cleansia.test", "Seed-Password-123", "Credit", "Tester");
        ctx.Languages.Add(Language.Create("en", "English"));
        ctx.Currencies.Add(currency);
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);

        var account = CreditAccount.Create(user.Id, currency.Id, ActorId);
        if (balance > 0m)
        {
            account.Issue(balance, CreditTransactionReason.Goodwill, "seed:" + account.Id, ActorId);
        }

        await using var seedCtx = NewContext();
        seedCtx.CreditAccounts.Add(account);
        await seedCtx.CommitAsync(CancellationToken.None);
        return account.Id;
    }

    private async Task<decimal> BalanceAsync(string accountId)
    {
        await using var ctx = NewContext();
        return (await ctx.CreditAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId)).Balance;
    }

    private async Task<(decimal Ledger, int Rows)> LedgerAsync(string accountId)
    {
        await using var ctx = NewContext();
        var rows = await ctx.CreditTransactions.AsNoTracking()
            .Where(t => t.CreditAccountId == accountId)
            .ToListAsync();
        return (rows.Sum(r => r.Amount), rows.Count);
    }

    private async Task<bool> DebitAsync(string accountId, decimal amount, string key)
    {
        await using var ctx = NewContext();
        return await new CreditAccountRepository(ctx).TryDebitAsync(
            accountId, amount, CreditTransactionReason.OrderPayment, key, ActorId,
            CancellationToken.None);
    }

    [Fact]
    public async Task ASpendWithinTheBalance_TakesTheMoneyAndRecordsIt()
    {
        await ResetAsync();
        var accountId = await SeedAccountAsync(500m);

        Assert.True(await DebitAsync(accountId, 200m, "spend-1"));

        Assert.Equal(300m, await BalanceAsync(accountId));
        var (ledger, rows) = await LedgerAsync(accountId);
        Assert.Equal(2, rows);       // the seed issue, and the spend
        Assert.Equal(300m, ledger);  // 500 - 200
    }

    /// <summary>
    /// THE INVARIANT, and the whole reason the ledger row moved inside the statement: the balance and
    /// the sum of the ledger are the same number, always, with no caller obliged to remember anything.
    /// </summary>
    [Fact]
    public async Task TheBalanceAlwaysEqualsTheLedger()
    {
        await ResetAsync();
        var accountId = await SeedAccountAsync(500m);

        await DebitAsync(accountId, 120m, "spend-a");
        await DebitAsync(accountId, 80m, "spend-b");

        var (ledger, _) = await LedgerAsync(accountId);
        Assert.Equal(await BalanceAsync(accountId), ledger);
        Assert.Equal(300m, ledger);
    }

    [Fact]
    public async Task ASpendBeyondTheBalance_TakesNothingAndRecordsNothing()
    {
        await ResetAsync();
        var accountId = await SeedAccountAsync(200m);

        Assert.False(await DebitAsync(accountId, 500m, "too-much"));

        Assert.Equal(200m, await BalanceAsync(accountId));
        var (_, rows) = await LedgerAsync(accountId);
        Assert.Equal(1, rows);  // only the seed issue - insufficient funds writes NO ledger row
    }

    /// <summary>
    /// Two spends of the whole balance; exactly one may win. A read-then-subtract in the application
    /// layer could not guarantee this, and no unique index can arbitrate it, because a balance has no
    /// key for two spends to collide on.
    /// </summary>
    [Fact]
    public async Task TheSameMoneyCannotBeSpentTwice()
    {
        await ResetAsync();
        var accountId = await SeedAccountAsync(500m);

        var first = await DebitAsync(accountId, 500m, "race-1");
        var second = await DebitAsync(accountId, 500m, "race-2");

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(0m, await BalanceAsync(accountId));
        Assert.Equal(0m, (await LedgerAsync(accountId)).Ledger);
    }

    /// <summary>
    /// A replayed spend is refused by the ledger's unique idempotency key, and the decrement rolls
    /// back with it — one statement, so there is no half-applied outcome to clean up.
    /// </summary>
    [Fact]
    public async Task AReplayedSpend_IsRefusedAndTakesNothing()
    {
        await ResetAsync();
        var accountId = await SeedAccountAsync(500m);

        Assert.True(await DebitAsync(accountId, 100m, "same-key"));
        await Assert.ThrowsAnyAsync<Exception>(() => DebitAsync(accountId, 100m, "same-key"));

        Assert.Equal(400m, await BalanceAsync(accountId));
        Assert.Equal(400m, (await LedgerAsync(accountId)).Ledger);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ANonPositiveAmount_IsRefused(decimal amount)
    {
        await ResetAsync();
        var accountId = await SeedAccountAsync(500m);

        // Without this guard a negative "spend" would be an UPDATE that INCREASES the balance.
        Assert.False(await DebitAsync(accountId, amount, "bad-amount"));
        Assert.Equal(500m, await BalanceAsync(accountId));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
