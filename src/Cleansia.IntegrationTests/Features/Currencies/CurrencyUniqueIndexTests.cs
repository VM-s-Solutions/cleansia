using Cleansia.Infra.Common.Validations;
using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Currencies;

/// <summary>
/// The two indexes <c>Currencies</c> has never had, against REAL Postgres — <c>Code</c> unique, and
/// <c>IsDefault</c> filtered-unique so exactly one row can be the platform default.
///
/// <para><b>Why real Postgres.</b> Not because SQLite ignores partial indexes — it does not, it has
/// supported and enforced them since 3.8 and EF emits the filter for it. It is the CASE-INSENSITIVITY
/// that only Postgres can answer: <c>Code</c> is <c>citext</c>, which SQLite maps to plain TEXT
/// affinity, so a lowercase duplicate is accepted there and rejected here. And the statement ORDERING
/// below is a real-database property that no in-memory fixture settles.</para>
///
/// <para><b>The condition on the change.</b> A partial unique index is an index, not a constraint, so
/// Postgres cannot defer it — it is checked at the end of every statement, and the instant both rows
/// read true is a violation rather than an intermediate state. Leaving both writes to the pipeline's
/// single commit emits two UPDATEs whose order EF Core does not guarantee. If the promote goes first
/// the admin's set-default star returns a 500. <see cref="Promoting_A_Second_Currency_Succeeds"/> is
/// what says the flush actually fixed that, rather than the index simply never being exercised.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CurrencyUniqueIndexTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
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

    private static Currency Czk(bool isDefault = false)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.SetAsDefault(isDefault);
        return currency;
    }

    private static Currency Eur(bool isDefault = false)
    {
        var currency = Currency.Create("EUR", "€", "Euro");
        currency.IsActive = true;
        currency.SetAsDefault(isDefault);
        return currency;
    }

    private async Task SeedAsync(params Currency[] currencies)
    {
        await using var ctx = NewContext();
        ctx.Currencies.AddRange(currencies);
        await ctx.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// One priced service in each currency given. The promote handler refuses a currency the catalogue
    /// has no rows in (an active, empty default would withhold every entry from every customer), so
    /// the cases that promote through the REAL handler need their target priced -- the index
    /// behaviour they exist to prove is downstream of that gate.
    /// </summary>
    private async Task PriceAsync(params Currency[] currencies)
    {
        await using var ctx = NewContext();
        var category = ServiceCategory.Create("cat", "Category", "d");
        var service = Service.Create(category.Id, "Service", "d", 60);
        ctx.Add(category);
        ctx.Add(service);
        foreach (var currency in currencies)
        {
            ctx.Add(ServicePrice.Create(service.Id, currency.Id, 100m, 0m));
        }
        await ctx.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// Anti-vacuity first: the fixture really does persist two currencies and one default, so the two
    /// refusals below are the index rejecting something rather than the arrangement failing.
    /// </summary>
    [Fact]
    public async Task Two_Distinct_Codes_With_One_Default_Are_Accepted()
    {
        await ResetAsync();
        await SeedAsync(Czk(isDefault: true), Eur());

        await using var ctx = NewContext();
        Assert.Equal(2, await ctx.Currencies.CountAsync());
        Assert.Equal("CZK", (await ctx.Currencies.SingleAsync(c => c.IsDefault)).Code);
    }

    [Fact]
    public async Task A_Duplicate_Code_Is_Refused()
    {
        await ResetAsync();
        await SeedAsync(Czk(isDefault: true));

        await using var ctx = NewContext();
        ctx.Currencies.Add(Czk());

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => ctx.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// <c>Code</c> is <c>citext</c>, so the uniqueness is case-insensitive — which is what a currency
    /// code means. Without this the duplicate above could be sidestepped by typing it in lower case,
    /// and <c>GetByCodeAsync</c>'s unordered <c>FirstOrDefault</c> would then decide which row stamps
    /// a cleaner's payout invoice.
    /// </summary>
    [Fact]
    public async Task A_Duplicate_Code_In_Different_Case_Is_Also_Refused()
    {
        await ResetAsync();
        await SeedAsync(Czk(isDefault: true));

        await using var ctx = NewContext();
        ctx.Currencies.Add(Currency.Create("czk", "Kč", "Czech koruna lower"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => ctx.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task A_Second_Default_Is_Refused()
    {
        await ResetAsync();
        await SeedAsync(Czk(isDefault: true));

        await using var ctx = NewContext();
        ctx.Currencies.Add(Eur(isDefault: true));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => ctx.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// The filter is what keeps this an index over ONE row rather than over the column: any number of
    /// non-default currencies coexist, because <c>WHERE "IsDefault" = true</c> excludes them from the
    /// index entirely. Without the filter, the second non-default row would collide with the first.
    /// </summary>
    [Fact]
    public async Task Many_NonDefault_Currencies_Coexist()
    {
        await ResetAsync();
        await SeedAsync(Czk(isDefault: true), Eur(), Currency.Create("PLN", "zł", "Polish złoty"));

        await using var ctx = NewContext();
        Assert.Equal(2, await ctx.Currencies.CountAsync(c => !c.IsDefault));
    }

    /// <summary>
    /// THE CONDITION. The real handler, against the live index — the case the index would break if the
    /// clear and the promote were left to one unordered batch. Run through the real repository with no
    /// ambient transaction, exactly as the pipeline runs it.
    /// </summary>
    [Fact]
    public async Task Promoting_A_Second_Currency_Succeeds()
    {
        await ResetAsync();

        var eur = Eur();
        await SeedAsync(Czk(isDefault: true), eur);
        await PriceAsync(eur);

        await using (var ctx = NewContext())
        {
            var result = await new SetDefaultCurrency.Handler(new CurrencyRepository(ctx))
                .Handle(new SetDefaultCurrency.Command(eur.Id), CancellationToken.None);

            Assert.True(result.IsSuccess, $"SetDefaultCurrency failed with: {result.Error?.Message}");
        }

        await using var verify = NewContext();
        Assert.Equal("EUR", (await verify.Currencies.SingleAsync(c => c.IsDefault)).Code);
        Assert.False(await verify.Currencies.AnyAsync(c => c.Code == "CZK" && c.IsDefault));
    }

    /// <summary>
    /// And it is still idempotent. Not because a self-collision is possible — the two reads resolve to
    /// the SAME tracked instance, so a clear-then-set of one row would simply end up true — but
    /// because the early return is what keeps the handler from opening a transaction and writing two
    /// UPDATEs for a request that changes nothing.
    /// </summary>
    [Fact]
    public async Task Promoting_The_Current_Default_Is_A_No_Op()
    {
        await ResetAsync();

        var czk = Czk(isDefault: true);
        await SeedAsync(czk, Eur());

        await using (var ctx = NewContext())
        {
            var result = await new SetDefaultCurrency.Handler(new CurrencyRepository(ctx))
                .Handle(new SetDefaultCurrency.Command(czk.Id), CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        await using var verify = NewContext();
        Assert.Equal("CZK", (await verify.Currencies.SingleAsync(c => c.IsDefault)).Code);
    }


    /// <summary>
    /// ANTI-VACUITY FOR THE FLUSH, and the reason the handler breaks the "never commit in a handler"
    /// rule. This is the OLD shape — clear and promote both mutated on tracked entities and left to one
    /// commit — and it is REJECTED.
    ///
    /// <para><b>And it is not a coin flip.</b> EF Core emits the two UPDATEs in the order the entities
    /// entered the CHANGE TRACKER, not in the order the handler mutated them — so which statement runs
    /// first is decided by which row was loaded first. The handler loads the promote target first
    /// (<c>GetByIdAsync</c> on the requested id) and the current default second
    /// (<c>GetDefaultAsync</c>), so the promote is always emitted first and two rows are always
    /// momentarily default. The old shape did not fail intermittently; it could not succeed at all.</para>
    ///
    /// <para>The arrangement below reproduces that load order exactly — the promoted row is queried
    /// first — which is why the mutation statements read in the opposite order to the queries.</para>
    ///
    /// <para>Without this, <see cref="The_Handler_Promotes_Twice_In_A_Row"/> proves nothing — it would
    /// pass against the old handler too, for the direction that happens to be ordered correctly.</para>
    /// </summary>
    [Fact]
    public async Task An_Unflushed_ClearThenSet_Fails_When_The_Promote_Is_Loaded_First()
    {
        await ResetAsync();

        var czk = Czk(isDefault: true);
        var eur = Eur();
        await SeedAsync(czk, eur);

        // CZK -> EUR, with the CLEAR loaded first. That entry order puts the clear's UPDATE first, so
        // the index never sees two true rows and the commit succeeds — which is why the shape looks
        // correct until something loads the rows the other way round.
        await using (var first = NewContext())
        {
            var from = await first.Currencies.SingleAsync(c => c.Code == "CZK");
            var to = await first.Currencies.SingleAsync(c => c.Code == "EUR");
            from.SetAsDefault(false);
            to.SetAsDefault(true);
            await first.CommitAsync(CancellationToken.None);
        }

        // EUR -> CZK, with the PROMOTE TARGET loaded first, exactly as the handler loads it. That
        // entry order is what puts its UPDATE first, and at the end of that statement both rows are
        // default.
        await using var second = NewContext();
        var lower = await second.Currencies.SingleAsync(c => c.Code == "CZK");
        var higher = await second.Currencies.SingleAsync(c => c.Code == "EUR");
        higher.SetAsDefault(false);
        lower.SetAsDefault(true);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
        Assert.Contains("IX_Currencies_IsDefault_Unique", ex.InnerException?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// The same two promotes, through the real handler — both succeed. This is the pair that says the
    /// flush is doing the work rather than the index simply never being exercised.
    /// </summary>
    [Fact]
    public async Task The_Handler_Promotes_Twice_In_A_Row()
    {
        await ResetAsync();

        var czk = Czk(isDefault: true);
        var eur = Eur();
        await SeedAsync(czk, eur);
        await PriceAsync(czk, eur);

        foreach (var targetId in new[] { eur.Id, czk.Id })
        {
            await using var ctx = NewContext();
            var result = await new SetDefaultCurrency.Handler(new CurrencyRepository(ctx))
                .Handle(new SetDefaultCurrency.Command(targetId), CancellationToken.None);

            Assert.True(result.IsSuccess, $"SetDefaultCurrency failed with: {result.Error?.Message}");
        }

        await using var verify = NewContext();
        Assert.Equal("CZK", (await verify.Currencies.SingleAsync(c => c.IsDefault)).Code);
    }


    /// <summary>
    /// The loser of a create race gets the business error the client already knows how to render, not a
    /// 500. The validator catches an ordinary duplicate, so the only way to reach the index is for two
    /// creates to both pass that check — which is what calling the handler directly reproduces.
    ///
    /// <para>Worth being precise about why this mattered: an unhandled 23505 leaves the exception
    /// handler writing a PLAIN TEXT 500 body, which the client's error interceptor cannot parse, so the
    /// admin sees the generic "An error occurred" while the correct translated key sits unused in all
    /// five locales.</para>
    /// </summary>
    [Fact]
    public async Task A_Create_That_Loses_The_Race_Returns_A_Business_Error()
    {
        await ResetAsync();
        await SeedAsync(Czk(isDefault: true));

        await using var ctx = NewContext();
        var result = await CreateAsync(ctx, new CreateCurrency.Command("CZK", "Kč", "Czech koruna"));

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.CurrencyCodeAlreadyExists, result.Error?.Message);
    }

    /// <summary>
    /// A code is stored CANONICAL, not merely unique-up-to-case. <c>citext</c> folds for comparison and
    /// stores what was typed, and that string is passed straight through as the receipt's currency code
    /// and onto the request sent to the tax authority — so "czk" would be wrong on a statutory document
    /// even though the index correctly refuses a second row beside it.
    /// </summary>
    [Fact]
    public async Task A_Lowercase_Code_Is_Stored_Uppercase()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            var result = await CreateAsync(ctx, new CreateCurrency.Command(" pln ", "zł", "Polish złoty"));

            Assert.True(result.IsSuccess, $"CreateCurrency failed with: {result.Error?.Message}");
        }

        await using var verify = NewContext();
        Assert.Equal("PLN", (await verify.Currencies.SingleAsync()).Code);
    }

    /// <summary>
    /// <c>CreateCurrency.Handler</c> is internal and no project has InternalsVisibleTo, so it is built
    /// reflectively — the same way GetPagedServicesHandlerTests reaches its handler.
    /// </summary>
    private static Task<BusinessResult<CreateCurrency.Response>> CreateAsync(
        CleansiaDbContext ctx, CreateCurrency.Command command)
    {
        var handlerType = typeof(CreateCurrency).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, new CurrencyRepository(ctx))!;
        return (Task<BusinessResult<CreateCurrency.Response>>)handlerType
            .GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

}
