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
/// One credit account per customer PER CURRENCY, against real Postgres.
///
/// <para><b>What this closes.</b> A balance is denominated, and the table held one row per customer —
/// so every currency's money was forced onto whichever account the customer's first credit happened to
/// open. The repository's own doc already promised the opposite ("an existing account keeps the
/// currency it was opened in") while the query behind it matched on <c>UserId</c> alone and handed back
/// an account in a different currency for the caller to add to. Nothing failed; the balance was just
/// wrong. That is why it needed a schema constraint and not a code review.</para>
///
/// <para><b>Why real Postgres.</b> The unique index is the whole mechanism, and the returns go through
/// a raw CTE that no in-memory provider runs.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CreditPerCurrencyTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string ActorId = "01ADMINCREDIT0000000000002";

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

    /// <summary>One customer, two currencies. Returns (userId, czkId, eurId).</summary>
    private async Task<(string UserId, string Czk, string Eur)> SeedAsync()
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));

        var czk = Currency.Create("CZK", "Kc", "Czech koruna", 1.0m);
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "E", "Euro", 1.0m);
        ctx.Currencies.AddRange(czk, eur);

        var user = User.CreateWithPassword(
            "credit-percurrency@cleansia.test", "Seed-Password-123", "Credit", "Tester");
        ctx.Users.Add(user);

        await ctx.CommitAsync(CancellationToken.None);
        return (user.Id, czk.Id, eur.Id);
    }

    private async Task GrantAsync(string userId, string currencyId, decimal amount, string key)
    {
        await using var ctx = NewContext();
        var account = await new CreditAccountRepository(ctx)
            .EnsureForUserAsync(userId, currencyId, CancellationToken.None);
        account.Issue(amount, CreditTransactionReason.Goodwill, key, ActorId, note: "seed");
        await ctx.CommitAsync(CancellationToken.None);
    }

    // ── the constraint ──────────────────────────────────────────────────────

    [Fact]
    public async Task One_Customer_Holds_One_Account_Per_Currency()
    {
        await ResetAsync();
        var (userId, czk, eur) = await SeedAsync();

        await GrantAsync(userId, czk, 400m, "grant-czk");
        await GrantAsync(userId, eur, 25m, "grant-eur");

        await using var ctx = NewContext();
        var accounts = await ctx.CreditAccounts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Balance)
            .ToListAsync();

        Assert.Equal(2, accounts.Count);
        Assert.Equal([25m, 400m], accounts.Select(a => a.Balance));
        Assert.Equal([eur, czk], accounts.Select(a => a.CurrencyId));
    }

    /// <summary>
    /// And the pair is still unique. Two accounts in the SAME currency is the state the old index
    /// forbade and the new one must keep forbidding — otherwise the conversion traded one wrong answer
    /// for an unbounded number of them.
    /// </summary>
    [Fact]
    public async Task A_Second_Account_In_The_Same_Currency_Is_Refused()
    {
        await ResetAsync();
        var (userId, czk, _) = await SeedAsync();
        await GrantAsync(userId, czk, 400m, "grant-czk");

        await using var ctx = NewContext();
        ctx.CreditAccounts.Add(CreditAccount.Create(userId, czk, ActorId));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => ctx.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
    }

    // ── the money ───────────────────────────────────────────────────────────

    /// <summary>
    /// THE DEFECT, stated as an outcome. A return denominated in one currency must not land on a
    /// balance held in another. Before the currency term, <c>EnsureForUserAsync</c> matched on user
    /// alone, so the CZK return below would have been added to the EUR balance — 400 EUR where the
    /// customer had 25.
    /// </summary>
    [Fact]
    public async Task A_Return_Lands_On_The_Account_In_Its_Own_Currency()
    {
        await ResetAsync();
        var (userId, czk, eur) = await SeedAsync();

        // The EUR account exists FIRST, so an unkeyed lookup would resolve to it.
        await GrantAsync(userId, eur, 25m, "grant-eur");

        await using (var ctx = NewContext())
        {
            var returned = await new CreditAccountRepository(ctx).TryReturnAsync(
                userId, czk, 400m, "return-czk", ActorId, CancellationToken.None);
            Assert.True(returned);
        }

        await using var verify = NewContext();
        var accounts = await verify.CreditAccounts.AsNoTracking()
            .Where(a => a.UserId == userId).ToListAsync();

        Assert.Equal(400m, Assert.Single(accounts, a => a.CurrencyId == czk).Balance);
        Assert.Equal(25m, Assert.Single(accounts, a => a.CurrencyId == eur).Balance);
    }

    /// <summary>
    /// The checkout read is keyed too. A customer holding a matching balance behind a non-matching one
    /// used to be told they had nothing spendable, because the read took an arbitrary account and the
    /// caller then compared its currency and gave up.
    /// </summary>
    [Fact]
    public async Task The_Spendable_Read_Finds_The_Matching_Currency()
    {
        await ResetAsync();
        var (userId, czk, eur) = await SeedAsync();

        await GrantAsync(userId, eur, 900m, "grant-eur");
        await GrantAsync(userId, czk, 400m, "grant-czk");

        await using var ctx = NewContext();
        var repository = new CreditAccountRepository(ctx);

        var forCzk = await repository.GetSpendableAsync(userId, czk, CancellationToken.None);
        var forEur = await repository.GetSpendableAsync(userId, eur, CancellationToken.None);

        Assert.Equal(400m, forCzk?.Balance);
        Assert.Equal(900m, forEur?.Balance);
    }

    [Fact]
    public async Task The_Spendable_Read_Returns_Nothing_For_An_Unheld_Currency()
    {
        await ResetAsync();
        var (userId, czk, eur) = await SeedAsync();
        await GrantAsync(userId, czk, 400m, "grant-czk");

        await using var ctx = NewContext();
        Assert.Null(await new CreditAccountRepository(ctx)
            .GetSpendableAsync(userId, eur, CancellationToken.None));
    }

    // ── erasure ─────────────────────────────────────────────────────────────

    /// <summary>
    /// THE LEGAL-FACING ONE. Erasure is refused while the platform still owes money, and the gate used
    /// to ask through a single-account read — so a customer holding nothing in one currency and a
    /// positive balance in another would have passed it and been erased with the debt outstanding.
    /// Erasure is irreversible; this is the only thing in front of it.
    ///
    /// <para>Asserted through the repository the gate actually calls, with the ZERO account ordered
    /// first so an implementation that looks at one row picks the wrong one.</para>
    /// </summary>
    [Fact]
    public async Task A_Balance_In_Any_Currency_Is_Visible_To_The_Erasure_Gate()
    {
        await ResetAsync();
        var (userId, czk, eur) = await SeedAsync();

        // A drained CZK account and a funded EUR one.
        await GrantAsync(userId, czk, 400m, "grant-czk");
        await using (var drain = NewContext())
        {
            var account = await drain.CreditAccounts.SingleAsync(a => a.CurrencyId == czk);
            account.Drain(ActorId, DateTimeOffset.UtcNow);
            await drain.CommitAsync(CancellationToken.None);
        }
        await GrantAsync(userId, eur, 25m, "grant-eur");

        await using var ctx = NewContext();
        var spendables = await new CreditAccountRepository(ctx)
            .GetSpendablesForUserAsync(userId, CancellationToken.None);

        Assert.Equal(2, spendables.Count);
        Assert.True(spendables.Any(s => s.Balance > 0m), "the EUR balance must be visible to the gate");
        Assert.Equal(25m, Assert.Single(spendables, s => s.CurrencyId == eur).Balance);
    }

    /// <summary>
    /// Anti-vacuity for the gate: a customer who genuinely holds nothing produces no positive balance,
    /// so the assertion above is the EUR row being seen rather than the predicate always being true.
    /// </summary>
    [Fact]
    public async Task A_Customer_Who_Holds_Nothing_Blocks_Nothing()
    {
        await ResetAsync();
        var (userId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var spendables = await new CreditAccountRepository(ctx)
            .GetSpendablesForUserAsync(userId, CancellationToken.None);

        Assert.Empty(spendables);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
