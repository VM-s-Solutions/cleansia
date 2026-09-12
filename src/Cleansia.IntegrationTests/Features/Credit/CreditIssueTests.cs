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
/// Putting credit ON a balance, against real Postgres.
///
/// <para>Issuing is the safe direction — two concurrent grants both increase the balance and both are
/// correct — so it goes through the tracked graph rather than a conditional UPDATE. What it still
/// needs the database for is the retry: <c>CreditTransactions.IdempotencyKey</c> carries a PLAIN
/// unique index, and the whole point of that shape is that it enforces something in single-tenant
/// mode, where <c>TenantId</c> is null and a <c>(TenantId, …)</c> index enforces nothing at all.
/// SQLite cannot make that claim on our behalf.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CreditIssueTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
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

    private async Task<(string UserId, string CurrencyId)> SeedCustomerAsync()
    {
        await using var ctx = NewContext();
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.IsActive = true;
        var user = User.CreateWithPassword(
            "credit-issue@cleansia.test", "Seed-Password-123", "Credit", "Tester");
        ctx.Languages.Add(Language.Create("en", "English"));
        ctx.Currencies.Add(currency);
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);
        return (user.Id, currency.Id);
    }

    private async Task<decimal> BalanceAsync(string userId)
    {
        await using var ctx = NewContext();
        return (await ctx.CreditAccounts.AsNoTracking().SingleAsync(a => a.UserId == userId)).Balance;
    }

    /// <summary>
    /// A customer with no account is not a special case: the first grant opens one. Nothing reads the
    /// difference between "no account" and "an account holding nothing".
    /// </summary>
    [Fact]
    public async Task TheFirstGrantOpensTheAccount()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync();

        await using (var ctx = NewContext())
        {
            var repo = new CreditAccountRepository(ctx);
            var account = await repo.EnsureForUserAsync(userId, currencyId, CancellationToken.None);
            account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", ActorId,
                note: "Second clean in a row went wrong.");
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Equal(500m, await BalanceAsync(userId));
    }

    /// <summary>
    /// A second grant lands on the SAME account. EnsureForUserAsync finding an existing row rather
    /// than opening a second one is what keeps <c>GetSpendableAsync</c>'s single-row read honest.
    /// </summary>
    [Fact]
    public async Task ASecondGrantLandsOnTheSameAccount()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync();

        foreach (var (amount, key) in new[] { (500m, "grant-1"), (250m, "grant-2") })
        {
            await using var ctx = NewContext();
            var repo = new CreditAccountRepository(ctx);
            var account = await repo.EnsureForUserAsync(userId, currencyId, CancellationToken.None);
            account.Issue(amount, CreditTransactionReason.Goodwill, key, ActorId, note: "n");
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var read = NewContext();
        Assert.Equal(1, await read.CreditAccounts.CountAsync(a => a.UserId == userId));
        Assert.Equal(750m, await BalanceAsync(userId));
    }

    /// <summary>
    /// THE ONE THE INDEX EXISTS FOR: an admin double-clicks, or a proxy retries, and the customer is
    /// granted the money ONCE. The unique index on IdempotencyKey is the arbiter — the read-then-write
    /// above it cannot be, because both attempts read the same balance.
    ///
    /// <para>It carries no <c>TenantId</c> term and no filter, deliberately. LoyaltyTransactions took
    /// the (TenantId, IdempotencyKey) shape, and with TenantId null in production Postgres treats
    /// every row's key as distinct — the backstop its own comment names enforces nothing.</para>
    /// </summary>
    [Fact]
    public async Task AReplayedGrantIsRefusedAndTheMoneyIsGrantedOnce()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync();

        await using (var first = NewContext())
        {
            var repo = new CreditAccountRepository(first);
            var account = await repo.EnsureForUserAsync(userId, currencyId, CancellationToken.None);
            account.Issue(500m, CreditTransactionReason.Goodwill, "same-token", ActorId, note: "n");
            await first.CommitAsync(CancellationToken.None);
        }

        await using (var replay = NewContext())
        {
            var repo = new CreditAccountRepository(replay);
            var account = await repo.EnsureForUserAsync(userId, currencyId, CancellationToken.None);
            account.Issue(500m, CreditTransactionReason.Goodwill, "same-token", ActorId, note: "n");
            await Assert.ThrowsAsync<DbUpdateException>(
                () => replay.CommitAsync(CancellationToken.None));
        }

        Assert.Equal(500m, await BalanceAsync(userId));
    }

    /// <summary>
    /// Balance and ledger agree on the issue side too, so the invariant the spend path is built around
    /// holds across a customer's whole history rather than only its debits.
    /// </summary>
    [Fact]
    public async Task TheBalanceEqualsTheLedgerAcrossIssuesAndSpends()
    {
        await ResetAsync();
        var (userId, currencyId) = await SeedCustomerAsync();

        string accountId;
        await using (var ctx = NewContext())
        {
            var repo = new CreditAccountRepository(ctx);
            var account = await repo.EnsureForUserAsync(userId, currencyId, CancellationToken.None);
            account.Issue(800m, CreditTransactionReason.DisputeSettlement, "grant-1", ActorId, note: "n");
            await ctx.CommitAsync(CancellationToken.None);
            accountId = account.Id;
        }

        await using (var spend = NewContext())
        {
            var taken = await new CreditAccountRepository(spend).TryDebitAsync(
                accountId, 300m, CreditTransactionReason.OrderPayment, "order-payment-x", ActorId,
                CancellationToken.None, orderId: "01ORDERCREDIT0000000000001");
            Assert.True(taken);
        }

        await using var read = NewContext();
        var ledger = await read.CreditTransactions.AsNoTracking()
            .Where(t => t.CreditAccountId == accountId)
            .SumAsync(t => t.Amount);

        Assert.Equal(500m, ledger);
        Assert.Equal(500m, await BalanceAsync(userId));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
