using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Credit;

/// <summary>
/// Putting credit BACK, against real Postgres.
///
/// <para>Credit is spent by one path and returned by six — a customer cancel, an admin cancel, a full
/// or partial refund, a dispute resolution, two sweeps and an expired-session webhook. Every one of
/// them routes through <c>TryReturnAsync</c>, and the properties that make that safe are all the
/// database's: the return and its ledger row move in one statement, and a replayed key writes
/// nothing at all rather than raising a 23505 that would take the caller's own commit with it.</para>
///
/// <para>The return is deliberately NOT a tracked <c>Issue</c>. <c>CreateOrder</c> calls it to
/// compensate a failed Stripe dispatch, on a request that then returns a FAILURE — the pipeline
/// commits nothing, so a tracked write would be discarded, and flushing it by hand would persist the
/// half-built order sitting in the same context. Self-contained is the only shape that survives a
/// path which is about to roll back, and that is what these tests hold in place.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CreditReturnTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string ActorId = "01ADMINCREDIT0000000000001";
    private const string OrderId = "01ORDERCREDIT0000000000001";

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

    private async Task<(string UserId, string CurrencyId)> SeedCustomerAsync(decimal balance)
    {
        await using var ctx = NewContext();
        var currency = Currency.Create("CZK", "Kc", "Czech koruna", 1.0m);
        var user = User.CreateWithPassword(
            "credit-return@cleansia.test", "Seed-Password-123", "Credit", "Tester");
        ctx.Languages.Add(Language.Create("en", "English"));
        ctx.Currencies.Add(currency);
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);

        if (balance > 0m)
        {
            await using var seed = NewContext();
            var repo = new CreditAccountRepository(seed);
            var account = await repo.EnsureForUserAsync(user.Id, currency.Id, CancellationToken.None);
            account.Issue(balance, CreditTransactionReason.Goodwill, "seed-grant", ActorId, note: "n");
            await seed.CommitAsync(CancellationToken.None);
        }

        return (user.Id, currency.Id);
    }

    private async Task<decimal> BalanceAsync(string userId)
    {
        await using var ctx = NewContext();
        var account = await ctx.CreditAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.UserId == userId);
        return account?.Balance ?? 0m;
    }

    private async Task<(decimal Ledger, int Rows)> LedgerAsync(string userId)
    {
        await using var ctx = NewContext();
        var accountId = await ctx.CreditAccounts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.Id)
            .SingleOrDefaultAsync();
        if (accountId == null)
        {
            return (0m, 0);
        }

        var rows = await ctx.CreditTransactions.AsNoTracking()
            .Where(t => t.CreditAccountId == accountId)
            .ToListAsync();
        return (rows.Sum(r => r.Amount), rows.Count);
    }

    private async Task<bool> ReturnAsync(
        string userId, string currencyId, decimal amount, string key)
    {
        await using var ctx = NewContext();
        return await new CreditAccountRepository(ctx).TryReturnAsync(
            userId, currencyId, amount, key, ActorId, CancellationToken.None, orderId: OrderId);
    }

    [Fact]
    public async Task AReturn_PutsTheMoneyBackAndRecordsIt()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(200m);

        Assert.True(await ReturnAsync(userId, currencyId, 300m, "credit-return:refund:x"));

        Assert.Equal(500m, await BalanceAsync(userId));
        var (ledger, rows) = await LedgerAsync(userId);
        Assert.Equal(2, rows);       // the seed grant, and the return
        Assert.Equal(500m, ledger);
    }

    /// <summary>
    /// THE INVARIANT, on the return side too: balance and ledger are the same number, always. It holds
    /// because the UPDATE and the INSERT are one statement, not because any caller remembered to do
    /// both.
    /// </summary>
    [Fact]
    public async Task TheBalanceAlwaysEqualsTheLedger()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(1000m);

        await ReturnAsync(userId, currencyId, 120m, "credit-return:a");
        await ReturnAsync(userId, currencyId, 80m, "credit-return:b");

        var (ledger, _) = await LedgerAsync(userId);
        Assert.Equal(await BalanceAsync(userId), ledger);
        Assert.Equal(1200m, ledger);
    }

    /// <summary>
    /// THE ONE THE CALLERS DEPEND ON: a replayed return is a silent no-op, not an exception.
    ///
    /// <para>A re-driven refund, a re-delivered Stripe webhook and a sweep that reaches an order twice
    /// all arrive on a key that is already used — routinely, not exceptionally. If that raised a 23505
    /// it would surface inside <c>RefundService</c> as a failed refund for money that had already gone
    /// back, and the caller would tell the customer their refund did not work.</para>
    /// </summary>
    [Fact]
    public async Task AReplayedReturn_IsASilentNoOpAndPaysNothingTwice()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(0m);

        Assert.True(await ReturnAsync(userId, currencyId, 500m, "credit-return:same"));
        Assert.False(await ReturnAsync(userId, currencyId, 500m, "credit-return:same"));

        Assert.Equal(500m, await BalanceAsync(userId));
        Assert.Equal(500m, (await LedgerAsync(userId)).Ledger);
    }

    /// <summary>
    /// A customer who has never been credited has no account. The compensating return on a failed
    /// dispatch is the one caller that could reach that state, and it must not throw.
    /// </summary>
    [Fact]
    public async Task AReturnToACustomerWithNoAccount_OpensOne()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(0m);

        Assert.True(await ReturnAsync(userId, currencyId, 250m, "credit-return:first"));

        Assert.Equal(250m, await BalanceAsync(userId));
        Assert.Equal(1, (await LedgerAsync(userId)).Rows);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ANonPositiveReturn_IsRefused(decimal amount)
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(400m);

        // Without this guard a negative "return" would be an UPDATE that DECREASES the balance,
        // bypassing the conditional UPDATE that is the only sanctioned way credit is spent.
        Assert.False(await ReturnAsync(userId, currencyId, amount, "credit-return:bad"));
        Assert.Equal(400m, await BalanceAsync(userId));
    }

    /// <summary>
    /// A spend followed by its return leaves the customer exactly where they started — the round trip
    /// an abandoned checkout makes, and the one that used to end with the money simply gone.
    /// </summary>
    [Fact]
    public async Task SpendingThenReturning_LeavesTheBalanceUnchanged()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(800m);

        string accountId;
        await using (var read = NewContext())
        {
            accountId = (await read.CreditAccounts.AsNoTracking()
                .SingleAsync(a => a.UserId == userId)).Id;
        }

        await using (var spend = NewContext())
        {
            Assert.True(await new CreditAccountRepository(spend).TryDebitAsync(
                accountId, 640m, CreditTransactionReason.OrderPayment,
                $"order-payment-{OrderId}", ActorId, CancellationToken.None, orderId: OrderId));
        }

        Assert.Equal(160m, await BalanceAsync(userId));

        Assert.True(await ReturnAsync(
            userId, currencyId, 640m, $"credit-return:order-ended-unpaid:{OrderId}"));

        Assert.Equal(800m, await BalanceAsync(userId));
        Assert.Equal(800m, (await LedgerAsync(userId)).Ledger);
    }

    /// <summary>
    /// The per-order returned total, which is what stops a second refund recomputing the credit leg
    /// from the full applied amount and handing the customer their credit twice.
    /// </summary>
    [Fact]
    public async Task TheReturnedTotalCountsOnlyReturnsForThatOrder()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(100m);

        await ReturnAsync(userId, currencyId, 120m, "credit-return:one");
        await ReturnAsync(userId, currencyId, 80m, "credit-return:two");

        await using var ctx = NewContext();
        var repo = new CreditAccountRepository(ctx);

        // The seed GRANT is a positive row on the same account, and it must not be counted: the
        // question is how much of THIS ORDER has been unwound, not how much the customer has ever
        // been given.
        Assert.Equal(200m, await repo.GetReturnedTotalForOrderAsync(OrderId, CancellationToken.None));
        Assert.Equal(0m, await repo.GetReturnedTotalForOrderAsync("other-order", CancellationToken.None));
    }

    /// <summary>
    /// THE ONE THAT WOULD ROT SILENTLY. Both the debit and the return are raw SQL that bypasses the
    /// entity, so neither runs <c>CreditAccount.Touch()</c> — they set <c>ExpiresOn</c> themselves.
    ///
    /// <para>Get it wrong and nothing fails: a customer who spends credit today keeps an expiry date
    /// from a year ago, and the nightly sweep takes the rest of their balance. There is no exception,
    /// no log line and no test that would notice, which is exactly why this one exists.</para>
    /// </summary>
    [Fact]
    public async Task SpendingAndReturningBothPushTheExpiryOut()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync(800m);

        string accountId;
        DateTimeOffset? afterGrant;
        await using (var read = NewContext())
        {
            var account = await read.CreditAccounts.AsNoTracking().SingleAsync(a => a.UserId == userId);
            accountId = account.Id;
            afterGrant = account.ExpiresOn;
        }
        Assert.NotNull(afterGrant);

        // Push the date into the past, as a year of sitting still would.
        await using (var age = NewContext())
        {
            await age.Database.ExecuteSqlAsync(
                $"""UPDATE "CreditAccounts" SET "ExpiresOn" = NOW() - INTERVAL '1 day' WHERE "Id" = {accountId}""");
        }

        await using (var spend = NewContext())
        {
            Assert.True(await new CreditAccountRepository(spend).TryDebitAsync(
                accountId, 100m, CreditTransactionReason.OrderPayment,
                "order-payment-expiry-probe", ActorId, CancellationToken.None, orderId: OrderId));
        }

        await using (var read = NewContext())
        {
            var afterSpend = (await read.CreditAccounts.AsNoTracking()
                .SingleAsync(a => a.Id == accountId)).ExpiresOn;
            Assert.NotNull(afterSpend);
            Assert.True(
                afterSpend > DateTimeOffset.UtcNow.AddDays(1),
                $"spending must push the expiry out, but it is {afterSpend}");
        }

        // And again, for the return.
        await using (var age = NewContext())
        {
            await age.Database.ExecuteSqlAsync(
                $"""UPDATE "CreditAccounts" SET "ExpiresOn" = NOW() - INTERVAL '1 day' WHERE "Id" = {accountId}""");
        }

        Assert.True(await ReturnAsync(userId, currencyId, 100m, "credit-return:expiry-probe"));

        await using (var read = NewContext())
        {
            var afterReturn = (await read.CreditAccounts.AsNoTracking()
                .SingleAsync(a => a.Id == accountId)).ExpiresOn;
            Assert.NotNull(afterReturn);
            Assert.True(
                afterReturn > DateTimeOffset.UtcNow.AddDays(1),
                $"a return must push the expiry out, but it is {afterReturn}");
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
