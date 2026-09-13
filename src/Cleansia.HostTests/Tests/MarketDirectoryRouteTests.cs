using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The market directory (ADR-0058 D1–D2) and the per-market copy figures (ADR-0060) end to end:
/// the anonymous <c>Market/GetOverview</c> read on the Customer host, and the admin writers that
/// feed it — the insurance ceiling on the country, the apology credit on the currency, and the
/// servicing gate that keeps an unpriceable country out of the wizard's address step.
/// </summary>
public sealed class MarketDirectoryRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AdminUserId = "u-admin-market";
    private const string AdminEmail = "admin-market@hosttests.local";

    private const string CzkId = "cur-czk-market";
    private const string EurId = "cur-eur-market";
    private const string CzeId = "country-cze-market";
    private const string SvkId = "country-svk-market";
    private const string DeuId = "country-deu-market";
    private const string PolId = "country-pol-market";

    private static string AdminToken() =>
        TestJwtFactory.Mint(AdminAudience, AdminUserId, AdminEmail, UserProfile.Administrator);

    private static Currency NewCurrency(string id, string code, bool isActive, bool isDefault, decimal? noShowCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.IsActive = isActive;
        currency.SetAsDefault(isDefault);
        currency.SetLoyaltyPointsDivisor(10m);
        currency.SetNoShowCredit(noShowCredit);
        return currency;
    }

    private static Country NewCountry(string id, string name, string iso3, string iso2, bool isServiced)
    {
        var country = Country.Create(name, iso3, iso2, isServiced);
        country.Id = id;
        return country;
    }

    /// <summary>
    /// The DEV seed's shape: CZE serviced on active default CZK with the 250 apology and the
    /// default-market flag; SVK serviced on EUR which is present but not switched on; POL serviced
    /// with no configuration at all.
    /// </summary>
    private Task SeedDevShapeAsync(bool eurActive = false) => SeedAsync(ctx =>
    {
        ctx.Currencies.AddRange(
            NewCurrency(CzkId, "CZK", isActive: true, isDefault: true, noShowCredit: 250m),
            NewCurrency(EurId, "EUR", isActive: eurActive, isDefault: false, noShowCredit: null));
        ctx.Countries.AddRange(
            NewCountry(CzeId, "Czechia", "CZE", "CZ", isServiced: true),
            NewCountry(SvkId, "Slovakia", "SVK", "SK", isServiced: true),
            NewCountry(PolId, "Poland", "POL", "PL", isServiced: true));
        ctx.CountryConfigurations.AddRange(
            CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m).AssignOperator(HostTestTenants.Default).SetAsDefaultMarket(true),
            CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.Default));
        return Task.CompletedTask;
    });

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private async Task<JsonElement> ReadMarketsAsync()
    {
        var resp = await CustomerClientAnonymous().GetAsync("/api/Market/GetOverview");
        HttpAssert.IsOk(resp);
        return await BodyAsync(resp);
    }

    [Fact]
    public async Task An_anonymous_customer_reads_the_ready_markets_with_their_figures()
    {
        await SeedDevShapeAsync();

        var markets = await ReadMarketsAsync();

        var row = Assert.Single(markets.EnumerateArray());
        Assert.Equal(CzeId, row.GetProperty("countryId").GetString());
        Assert.Equal("CZE", row.GetProperty("isoCode").GetString());
        Assert.Equal("CZ", row.GetProperty("isoAlpha2").GetString());
        Assert.Equal(CzkId, row.GetProperty("currencyId").GetString());
        Assert.Equal("CZK", row.GetProperty("currencyCode").GetString());
        Assert.True(row.GetProperty("isDefault").GetBoolean());
        Assert.Equal(250m, row.GetProperty("noShowCredit").GetDecimal());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("insuranceCoverageAmount").ValueKind);
    }

    /// <summary>
    /// Several markets on the default currency is a legal state (EUR default, SVK and DEU on): one
    /// row is flagged, by lowest ISO code, and the read is still a 200 — it is the landing page's.
    /// </summary>
    [Fact]
    public async Task Two_markets_on_the_default_currency_flag_exactly_one_by_lowest_iso_code()
    {
        await SeedAsync(ctx =>
        {
            ctx.Currencies.AddRange(
                NewCurrency(EurId, "EUR", isActive: true, isDefault: true, noShowCredit: null),
                NewCurrency(CzkId, "CZK", isActive: true, isDefault: false, noShowCredit: 250m));
            ctx.Countries.AddRange(
                NewCountry(SvkId, "Slovakia", "SVK", "SK", isServiced: true),
                NewCountry(DeuId, "Germany", "DEU", "DE", isServiced: true),
                NewCountry(CzeId, "Czechia", "CZE", "CZ", isServiced: true));
            ctx.CountryConfigurations.AddRange(
                CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.Default),
                CountryConfiguration.Create(DeuId, "EUR", "de", 0.19m).AssignOperator(HostTestTenants.Default),
                CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m).AssignOperator(HostTestTenants.Default));
            return Task.CompletedTask;
        });

        var markets = await ReadMarketsAsync();

        var rows = markets.EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        var flagged = Assert.Single(rows, r => r.GetProperty("isDefault").GetBoolean());
        Assert.Equal("DEU", flagged.GetProperty("isoCode").GetString());
    }

    [Fact]
    public async Task No_market_on_the_default_currency_flags_nothing_and_still_answers()
    {
        await SeedAsync(ctx =>
        {
            ctx.Currencies.AddRange(
                NewCurrency(CzkId, "CZK", isActive: true, isDefault: true, noShowCredit: 250m),
                NewCurrency(EurId, "EUR", isActive: true, isDefault: false, noShowCredit: null));
            ctx.Countries.AddRange(
                NewCountry(CzeId, "Czechia", "CZE", "CZ", isServiced: false),
                NewCountry(SvkId, "Slovakia", "SVK", "SK", isServiced: true));
            ctx.CountryConfigurations.AddRange(
                CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m).AssignOperator(HostTestTenants.Default),
                CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.Default));
            return Task.CompletedTask;
        });

        var markets = await ReadMarketsAsync();

        var row = Assert.Single(markets.EnumerateArray());
        Assert.Equal("SVK", row.GetProperty("isoCode").GetString());
        Assert.False(row.GetProperty("isDefault").GetBoolean());
    }

    /// <summary>
    /// Owner ruling 2026-09-13: the default market is an explicit flag the admin moves.
    /// SVK is on EUR, not the default currency, and still becomes the pre-selection once flagged;
    /// exactly one configuration carries the flag afterwards, and both admin reads show it.
    /// </summary>
    [Fact]
    public async Task The_admin_moves_the_default_market_and_the_directory_pre_selects_it()
    {
        await SeedDevShapeAsync(eurActive: true);
        var admin = AdminClient(AdminToken());

        var before = (await ReadMarketsAsync()).EnumerateArray().ToList();
        Assert.Equal("CZE", Assert.Single(before, r => r.GetProperty("isDefault").GetBoolean()).GetProperty("isoCode").GetString());

        HttpAssert.IsOk(await admin.PutAsJsonAsync($"/api/AdminCountry/{SvkId}/default-market", new { }));

        var after = (await ReadMarketsAsync()).EnumerateArray().ToList();
        Assert.Equal(2, after.Count);
        Assert.Equal("SVK", Assert.Single(after, r => r.GetProperty("isDefault").GetBoolean()).GetProperty("isoCode").GetString());

        var detail = await BodyAsync(await admin.GetAsync($"/api/AdminCountry/details/{SvkId}"));
        Assert.True(detail.GetProperty("isDefaultMarket").GetBoolean());
        var overview = (await BodyAsync(await admin.GetAsync("/api/AdminCountry/get-overview"))).EnumerateArray().ToList();
        Assert.Equal(SvkId, Assert.Single(overview, r => r.GetProperty("isDefaultMarket").GetBoolean()).GetProperty("id").GetString());

        var flagged = await QueryAsync(ctx => ctx.CountryConfigurations.IgnoreQueryFilters().Where(c => c.IsDefaultMarket).Select(c => c.CountryId).ToListAsync());
        Assert.Equal([SvkId], flagged);
    }

    /// <summary>
    /// The three ways a serviced country is not a market: no configuration, a configured currency that
    /// is not switched on, and nobody operating it (ADR-0061 D2) — the last would leave the operator
    /// resolver's default pointing at a market the directory does not list.
    /// </summary>
    [Fact]
    public async Task An_unready_country_cannot_become_the_default_market_and_the_flag_stays_put()
    {
        await SeedDevShapeAsync();
        await SeedAsync(ctx =>
        {
            ctx.Countries.Add(NewCountry(DeuId, "Germany", "DEU", "DE", isServiced: true));
            ctx.CountryConfigurations.Add(CountryConfiguration.Create(DeuId, "CZK", "de", 0.19m));
            return Task.CompletedTask;
        });
        var admin = AdminClient(AdminToken());

        var noConfiguration = await admin.PutAsJsonAsync($"/api/AdminCountry/{PolId}/default-market", new { });
        await HttpAssert.RejectedAsync(noConfiguration, BusinessErrorMessage.CountryMarketNotReady);

        var inactiveCurrency = await admin.PutAsJsonAsync($"/api/AdminCountry/{SvkId}/default-market", new { });
        await HttpAssert.RejectedAsync(inactiveCurrency, BusinessErrorMessage.CountryMarketNotReady);

        var noOperator = await admin.PutAsJsonAsync($"/api/AdminCountry/{DeuId}/default-market", new { });
        await HttpAssert.RejectedAsync(noOperator, BusinessErrorMessage.CountryMarketNotReady);

        var flagged = await QueryAsync(ctx => ctx.CountryConfigurations.IgnoreQueryFilters().Where(c => c.IsDefaultMarket).Select(c => c.CountryId).ToListAsync());
        Assert.Equal([CzeId], flagged);
    }

    [Fact]
    public async Task Setting_the_default_market_needs_the_country_permission()
    {
        await SeedDevShapeAsync();
        var customer = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-cust-market", "cust-market@hosttests.local", UserProfile.Customer));

        HttpAssert.IsForbidden(await customer.PutAsJsonAsync($"/api/AdminCountry/{CzeId}/default-market", new { }));
    }

    [Fact]
    public async Task The_insurance_ceiling_round_trips_from_the_admin_form_to_the_market_directory()
    {
        await SeedDevShapeAsync();
        var admin = AdminClient(AdminToken());

        var put = await admin.PutAsJsonAsync(
            $"/api/AdminCountry/{CzeId}/market-content",
            new { CountryId = CzeId, InsuranceCoverageAmount = 1_000_000m });
        HttpAssert.IsOk(put);

        var details = await admin.GetAsync($"/api/AdminCountry/details/{CzeId}");
        HttpAssert.IsOk(details);
        var detail = await BodyAsync(details);
        Assert.Equal(1_000_000m, detail.GetProperty("insuranceCoverageAmount").GetDecimal());
        Assert.True(detail.GetProperty("hasConfiguration").GetBoolean());
        Assert.Equal("CZ", detail.GetProperty("isoAlpha2").GetString());

        var market = Assert.Single((await ReadMarketsAsync()).EnumerateArray());
        Assert.Equal(1_000_000m, market.GetProperty("insuranceCoverageAmount").GetDecimal());
    }

    [Fact]
    public async Task Market_content_on_a_country_with_no_configuration_is_refused()
    {
        await SeedDevShapeAsync();

        var put = await AdminClient(AdminToken()).PutAsJsonAsync(
            $"/api/AdminCountry/{PolId}/market-content",
            new { CountryId = PolId, InsuranceCoverageAmount = 1_000_000m });

        await HttpAssert.RejectedAsync(put, BusinessErrorMessage.CountryConfigurationMissing);

        var details = await BodyAsync(await AdminClient(AdminToken()).GetAsync($"/api/AdminCountry/details/{PolId}"));
        Assert.False(details.GetProperty("hasConfiguration").GetBoolean());
        Assert.Equal(JsonValueKind.Null, details.GetProperty("insuranceCoverageAmount").ValueKind);
    }

    [Fact]
    public async Task Market_content_needs_the_country_permission()
    {
        await SeedDevShapeAsync();
        var customer = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-cust-market", "cust-market@hosttests.local", UserProfile.Customer));

        var put = await customer.PutAsJsonAsync(
            $"/api/AdminCountry/{CzeId}/market-content",
            new { CountryId = CzeId, InsuranceCoverageAmount = 1_000_000m });

        HttpAssert.IsForbidden(put);
    }

    /// <summary>ADR-0058 D7 gate 2: switching a country ON needs a configuration naming an ACTIVE currency; OFF is never gated.</summary>
    [Fact]
    public async Task Servicing_an_unready_country_is_refused_and_unservicing_never_is()
    {
        await SeedDevShapeAsync();
        var admin = AdminClient(AdminToken());

        var noConfiguration = await admin.PutAsJsonAsync($"/api/AdminCountry/{PolId}/serviced", new { IsServiced = true });
        await HttpAssert.RejectedAsync(noConfiguration, BusinessErrorMessage.CountryMarketNotReady);

        var inactiveCurrency = await admin.PutAsJsonAsync($"/api/AdminCountry/{SvkId}/serviced", new { IsServiced = true });
        await HttpAssert.RejectedAsync(inactiveCurrency, BusinessErrorMessage.CountryMarketNotReady);

        HttpAssert.IsOk(await admin.PutAsJsonAsync($"/api/AdminCountry/{SvkId}/serviced", new { IsServiced = false }));
        HttpAssert.IsOk(await admin.PutAsJsonAsync($"/api/AdminCountry/{CzeId}/serviced", new { IsServiced = true }));

        var slovakia = await QueryAsync(ctx => ctx.Countries.IgnoreQueryFilters().FirstAsync(c => c.Id == SvkId));
        Assert.False(slovakia.IsServiced);
    }

    [Fact]
    public async Task The_apology_credit_is_authored_per_currency_and_clears_to_null()
    {
        await SeedDevShapeAsync();
        var admin = AdminClient(AdminToken());

        var negative = await admin.PutAsJsonAsync(
            $"/api/AdminCurrency/update/{CzkId}",
            new { CurrencyId = CzkId, Code = "CZK", Symbol = "Kč", Name = "Czech koruna", LoyaltyPointsDivisor = 10m, NoShowCredit = -1m });
        await HttpAssert.RejectedAsync(negative, BusinessErrorMessage.MustBePositive);

        var cleared = await admin.PutAsJsonAsync(
            $"/api/AdminCurrency/update/{CzkId}",
            new { CurrencyId = CzkId, Code = "CZK", Symbol = "Kč", Name = "Czech koruna", LoyaltyPointsDivisor = 10m, NoShowCredit = (decimal?)null });
        HttpAssert.IsOk(cleared);

        var detail = await BodyAsync(await admin.GetAsync($"/api/AdminCurrency/details/{CzkId}"));
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("noShowCredit").ValueKind);

        var market = Assert.Single((await ReadMarketsAsync()).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, market.GetProperty("noShowCredit").ValueKind);
    }
}
