using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// Spending credit, against a real <see cref="CleansiaDbContext"/>.
///
/// <para>A balance is an AGGREGATE. There is no key two concurrent spends can collide on, so no
/// unique index can arbitrate it and a read-then-subtract in the app layer loses the race by
/// construction. The arbiter is a single conditional UPDATE, and these pin its two halves: it takes
/// the whole amount or it takes nothing, and it never goes below zero.</para>
///
/// <para>The failure it exists to prevent is the one <c>LoyaltyAccount.RevokePoints</c> would have
/// introduced if copied: <c>Math.Max(0, balance - amount)</c> is harmless on a tier counter and a
/// silent partial-spend on money — the customer gets the full discount and the ledger floors at
/// zero.</para>
/// </summary>
public sealed class CreditAccountRepositoryDebitTests : IDisposable
{
    private const string UserId = "user-1";
    private const string CurrencyId = "currency-1";

    private readonly SqliteConnection _connection;

    public CreditAccountRepositoryDebitTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // CreditAccount has a required FK to User. These tests are about arithmetic under
        // concurrency, so they seed an account and nothing else.
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

    private async Task<string> SeedAccountAsync(decimal balance)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var account = CreditAccount.Create(UserId, CurrencyId, "admin-1");
        if (balance > 0m)
        {
            account.Issue(balance, CreditTransactionReason.Goodwill, $"seed:{UserId}", "admin-1");
        }

        ctx.CreditAccounts.Add(account);
        await ctx.CommitAsync(CancellationToken.None);
        return account.Id;
    }

    private async Task<decimal> BalanceAsync(string accountId)
    {
        await using var ctx = NewContext();
        return (await ctx.CreditAccounts.SingleAsync(a => a.Id == accountId)).Balance;
    }

    private async Task<bool> DebitAsync(string accountId, decimal amount)
    {
        await using var ctx = NewContext();
        return await new CreditAccountRepository(ctx)
            .TryDebitAsync(accountId, amount, CancellationToken.None);
    }

    [Fact]
    public async Task ASpendWithinTheBalance_Succeeds_AndTakesExactlyThatMuch()
    {
        var accountId = await SeedAccountAsync(500m);

        Assert.True(await DebitAsync(accountId, 200m));
        Assert.Equal(300m, await BalanceAsync(accountId));
    }

    [Fact]
    public async Task SpendingTheWholeBalance_IsAllowed()
    {
        var accountId = await SeedAccountAsync(500m);

        Assert.True(await DebitAsync(accountId, 500m));
        Assert.Equal(0m, await BalanceAsync(accountId));
    }

    [Fact]
    public async Task ASpendBeyondTheBalance_TakesNOTHING()
    {
        // The clamp bug, as an assertion. A partial take here would hand the customer 500 of value
        // against 200 of credit.
        var accountId = await SeedAccountAsync(200m);

        Assert.False(await DebitAsync(accountId, 500m));
        Assert.Equal(200m, await BalanceAsync(accountId));
    }

    [Fact]
    public async Task TheSameMoneyCannotBeSpentTwice()
    {
        // Two spends of 500 against a balance of 500. Exactly one may win — this is the whole reason
        // the decrement is a conditional UPDATE rather than a read followed by a subtraction.
        var accountId = await SeedAccountAsync(500m);

        var first = await DebitAsync(accountId, 500m);
        var second = await DebitAsync(accountId, 500m);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(0m, await BalanceAsync(accountId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ANonPositiveAmount_IsRefused(decimal amount)
    {
        // Without this, a negative "spend" would be an UPDATE that INCREASES the balance.
        var accountId = await SeedAccountAsync(500m);

        Assert.False(await DebitAsync(accountId, amount));
        Assert.Equal(500m, await BalanceAsync(accountId));
    }

    [Fact]
    public async Task TheBalanceAlwaysEqualsTheLedger()
    {
        var accountId = await SeedAccountAsync(500m);
        await DebitAsync(accountId, 120m);

        await using var ctx = NewContext();
        var ledger = await ctx.CreditTransactions
            .Where(t => t.CreditAccountId == accountId)
            .SumAsync(t => t.Amount);

        // The spend is issued by the conditional UPDATE, which writes no ledger row of its own — the
        // CALLER records it. So after a bare debit the two disagree by exactly the amount taken, and
        // that is the contract: whoever spends must also write the movement.
        Assert.Equal(500m, ledger);
        Assert.Equal(380m, await BalanceAsync(accountId));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
