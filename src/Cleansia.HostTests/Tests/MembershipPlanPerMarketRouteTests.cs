using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Cleansia Plus priced per market (ADR-0059), end to end over the real hosts: the admin authors a
/// price per currency and reads it back keyed by code with no key for an unpriced currency; the
/// customer plan list answers per market; and a subscribe in a market where the plan is unpriced is
/// refused before any Stripe call.
/// </summary>
public sealed class MembershipPlanPerMarketRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AdminUserId = "u-admin-plus";
    private const string AdminEmail = "admin-plus@hosttests.local";
    private const string CustomerEmail = "customer-plus@hosttests.local";

    private const string EurId = "cur-eur-plus";
    private const string CzeId = "country-cze-plus";
    private const string SvkId = "country-svk-plus";
    private const string MonthlyId = "plan-monthly-plus";
    private const string YearlyId = "plan-yearly-plus";

    private static string AdminToken() =>
        TestJwtFactory.Mint(AdminAudience, AdminUserId, AdminEmail, UserProfile.Administrator);

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>CZE on the default CZK, SVK on an active EUR; both plans priced in CZK only.</summary>
    private Task SeedAsync() => SeedAsync(async ctx =>
    {
        await DomainSeed.EnsureReferenceDataAsync(ctx);

        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = EurId;
        eur.IsActive = true;
        eur.SetLoyaltyPointsDivisor(1m);
        ctx.Currencies.Add(eur);

        var cze = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        cze.Id = CzeId;
        var svk = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        svk.Id = SvkId;
        ctx.Countries.AddRange(cze, svk);
        ctx.CountryConfigurations.AddRange(
            CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m).AssignOperator(HostTestTenants.Default),
            CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.Default));

        var monthly = DomainSeed.MembershipPlan("PLUS_MONTHLY");
        monthly.Id = MonthlyId;
        var yearly = Cleansia.Core.Domain.Memberships.MembershipPlan.Create(
            "PLUS_YEARLY", "Host-test yearly", 10m, 24, true, BillingInterval.Yearly);
        yearly.Id = YearlyId;
        ctx.MembershipPlans.AddRange(monthly, yearly);
        ctx.MembershipPlanPrices.AddRange(
            DomainSeed.MembershipPlanPrice(MonthlyId, "PLUS_MONTHLY", 199m),
            DomainSeed.MembershipPlanPrice(YearlyId, "PLUS_YEARLY", 2030m));
    });

    [Fact]
    public async Task The_admin_detail_carries_the_czk_row_and_no_eur_key()
    {
        await SeedAsync();

        var resp = await AdminClient(AdminToken()).GetAsync($"/api/AdminMembership/details/{MonthlyId}");

        HttpAssert.IsOk(resp);
        var body = await BodyAsync(resp);
        var prices = body.GetProperty("prices");
        var czk = Assert.Single(prices.EnumerateObject());
        Assert.Equal("CZK", czk.Name);
        Assert.Equal(199m, czk.Value.GetProperty("price").GetDecimal());
        Assert.Equal(199m, czk.Value.GetProperty("monthlyEquivalentPrice").GetDecimal());
        Assert.Equal("price_hosttest_PLUS_MONTHLY", czk.Value.GetProperty("stripePriceId").GetString());
        Assert.False(prices.TryGetProperty("EUR", out _));
    }

    [Fact]
    public async Task A_benefit_edit_sending_only_czk_succeeds_and_leaves_eur_absent()
    {
        await SeedAsync();

        var resp = await AdminClient(AdminToken()).PutAsJsonAsync(
            $"/api/AdminMembership/update/{MonthlyId}",
            new
            {
                MembershipPlanId = MonthlyId,
                Name = "Host-test plan",
                Prices = new Dictionary<string, object> { ["CZK"] = new { Price = 199m, StripePriceId = "price_hosttest_PLUS_MONTHLY" } },
                DiscountPercentage = 12m,
                FreeCancellationWindowHours = 24,
                TrialPeriodDays = 0,
                AllowsExpressUpgrade = true,
                ExpressUpgradesPerMonth = 1,
            });

        HttpAssert.IsOk(resp);
        var rows = await QueryAsync(ctx => ctx.MembershipPlanPrices.Where(p => p.MembershipPlanId == MonthlyId).ToListAsync());
        var only = Assert.Single(rows);
        Assert.Equal(DomainSeed.CurrencyId, only.CurrencyId);
        var discount = await QueryAsync(ctx => ctx.MembershipPlans.Where(p => p.Id == MonthlyId).Select(p => p.DiscountPercentage).SingleAsync());
        Assert.Equal(12m, discount);
    }

    [Fact]
    public async Task An_unknown_currency_key_is_refused_and_no_plan_is_written()
    {
        await SeedAsync();

        var resp = await AdminClient(AdminToken()).PostAsJsonAsync(
            "/api/AdminMembership/create",
            new
            {
                Code = "PLUS_XXX",
                Name = "Nowhere",
                BillingInterval = 1,
                Prices = new Dictionary<string, object> { ["XXX"] = new { Price = 1m, StripePriceId = "price_xxx" } },
                DiscountPercentage = 5m,
                FreeCancellationWindowHours = 4,
                TrialPeriodDays = 0,
                AllowsExpressUpgrade = true,
            });

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.CurrencyNotFound);
        Assert.False(await QueryAsync(ctx => ctx.MembershipPlans.AnyAsync(p => p.Code == "PLUS_XXX")));
    }

    [Fact]
    public async Task A_stripe_price_already_charging_another_plan_is_refused()
    {
        await SeedAsync();

        var resp = await AdminClient(AdminToken()).PutAsJsonAsync(
            $"/api/AdminMembership/update/{MonthlyId}",
            new
            {
                MembershipPlanId = MonthlyId,
                Name = "Host-test plan",
                Prices = new Dictionary<string, object> { ["CZK"] = new { Price = 199m, StripePriceId = "price_hosttest_PLUS_YEARLY" } },
                DiscountPercentage = 10m,
                FreeCancellationWindowHours = 24,
                TrialPeriodDays = 0,
                AllowsExpressUpgrade = true,
            });

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }

    [Fact]
    public async Task The_customer_plan_list_is_empty_for_the_unpriced_market_and_labelled_for_the_priced_one()
    {
        await SeedAsync();
        var anonymous = CustomerClientAnonymous();

        var svk = await anonymous.GetAsync($"/api/Membership/GetPlans?countryId={SvkId}");
        var cze = await anonymous.GetAsync($"/api/Membership/GetPlans?countryId={CzeId}");
        var nowhere = await anonymous.GetAsync("/api/Membership/GetPlans?countryId=country-nowhere");

        HttpAssert.IsOk(svk);
        Assert.Empty((await BodyAsync(svk)).EnumerateArray());
        HttpAssert.IsOk(nowhere);
        Assert.Empty((await BodyAsync(nowhere)).EnumerateArray());
        HttpAssert.IsOk(cze);
        var rows = (await BodyAsync(cze)).EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("CZK", r.GetProperty("currencyCode").GetString()));
        var yearly = rows.Single(r => r.GetProperty("code").GetString() == "PLUS_YEARLY");
        Assert.Equal(2030m, yearly.GetProperty("price").GetDecimal());
        Assert.Equal(169.17m, yearly.GetProperty("monthlyEquivalentPrice").GetDecimal());
    }

    [Fact]
    public async Task A_subscribe_in_a_market_where_the_plan_is_unpriced_is_refused_with_the_named_key()
    {
        await SeedAsync();
        string customerId = "";
        await SeedAsync(ctx =>
        {
            var customer = DomainSeed.Customer(CustomerEmail);
            ctx.Users.Add(customer);
            customerId = customer.Id;
            return Task.CompletedTask;
        });
        var token = TestJwtFactory.Mint(CustomerAudience, customerId, CustomerEmail, UserProfile.Customer);

        var checkout = await CustomerClient(token).PostAsJsonAsync(
            "/api/Membership/CreateCheckoutSession", new { PlanCode = "PLUS_MONTHLY", CountryId = SvkId });
        var subscribe = await CustomerClient(token).PostAsJsonAsync(
            "/api/Membership/Subscribe", new { PlanCode = "PLUS_MONTHLY", PaymentMethodConfirmed = true, CountryId = SvkId });

        await HttpAssert.RejectedAsync(checkout, BusinessErrorMessage.MembershipPlanNotPricedInCurrency);
        await HttpAssert.RejectedAsync(subscribe, BusinessErrorMessage.MembershipPlanNotPricedInCurrency);
        Assert.False(await QueryAsync(ctx => ctx.UserMemberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == customerId)));
    }

    [Fact]
    public async Task A_subscribe_naming_an_unserviced_country_is_refused_by_the_validator()
    {
        await SeedAsync();
        string customerId = "";
        await SeedAsync(ctx =>
        {
            var customer = DomainSeed.Customer(CustomerEmail);
            ctx.Users.Add(customer);
            customerId = customer.Id;
            return Task.CompletedTask;
        });
        var token = TestJwtFactory.Mint(CustomerAudience, customerId, CustomerEmail, UserProfile.Customer);

        var checkout = await CustomerClient(token).PostAsJsonAsync(
            "/api/Membership/CreateCheckoutSession", new { PlanCode = "PLUS_MONTHLY", CountryId = "country-nowhere" });

        await HttpAssert.RejectedAsync(checkout, BusinessErrorMessage.CountryNotServiced);
    }
}
