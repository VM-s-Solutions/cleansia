using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Owner ruling 2026-09-13 (Q-MARKET-05), end to end on the Customer host with a recording Stripe
/// client: a customer whose CZK Plus was cancelled subscribes in EUR, and the subscription is created
/// on a SECOND Stripe Customer minted for EUR — the legacy Customer, locked to CZK by its first
/// invoice, is never asked to bill EUR. A later CZK subscribe adopts the legacy Customer for CZK.
/// </summary>
public sealed class MembershipResubscribeAcrossCurrenciesRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CustomerEmail = "resub@hosttests.local";
    private const string LegacyCustomerId = "cus_legacy_resub";
    private const string EurId = "cur-eur-resub";
    private const string CzeId = "country-cze-resub";
    private const string SvkId = "country-svk-resub";
    private const string PlanId = "plan-monthly-resub";

    private readonly RecordingStripeClient _stripe = new();

    protected override void ConfigureCustomerHostServices(IServiceCollection services)
    {
        services.RemoveAll<IStripeClient>();
        services.AddSingleton<IStripeClient>(_stripe);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>CZK default + EUR active; CZE and SVK serviced; the plan priced in both; a customer with a legacy Customer and a cancelled CZK membership.</summary>
    private async Task<string> SeedAsync()
    {
        var customerId = "";
        await SeedAsync(async ctx =>
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
                CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m),
                CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m));

            var plan = DomainSeed.MembershipPlan("PLUS_MONTHLY");
            plan.Id = PlanId;
            ctx.MembershipPlans.Add(plan);
            ctx.MembershipPlanPrices.AddRange(
                DomainSeed.MembershipPlanPrice(PlanId, "PLUS_MONTHLY", 199m),
                MembershipPlanPrice.Create(PlanId, EurId, 7.99m, "price_hosttest_PLUS_MONTHLY_eur"));

            var customer = DomainSeed.Customer(CustomerEmail);
            customer.AssignStripeCustomerId(LegacyCustomerId);
            ctx.Users.Add(customer);
            customerId = customer.Id;

            var cancelled = UserMembership.Create(
                customer.Id, PlanId, DomainSeed.CurrencyId, "sub_czk_cancelled_resub",
                DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(-1));
            cancelled.UpdateFromStripeWebhook("canceled", cancelled.CurrentPeriodStart, cancelled.CurrentPeriodEnd, null);
            ctx.UserMemberships.Add(cancelled);
        });
        return customerId;
    }

    [Fact]
    public async Task Subscribing_in_eur_after_a_cancelled_czk_membership_creates_a_second_stripe_customer()
    {
        var customerId = await SeedAsync();
        var client = CustomerClient(TestJwtFactory.Mint(CustomerAudience, customerId, CustomerEmail, UserProfile.Customer));

        var subscribe = await client.PostAsJsonAsync(
            "/api/Membership/Subscribe",
            new { PlanCode = "PLUS_MONTHLY", PaymentMethodConfirmed = true, CountryId = SvkId, IdempotencyToken = "resub-eur-1" });

        HttpAssert.IsOk(subscribe);
        var body = await BodyAsync(subscribe);
        Assert.Equal("cus_fake_1", body.GetProperty("stripeCustomerId").GetString());

        var minted = Assert.Single(_stripe.CreatedCustomers);
        Assert.Equal(customerId, minted.UserId);
        var subscription = Assert.Single(_stripe.CreatedSubscriptions);
        Assert.Equal(("cus_fake_1", "price_hosttest_PLUS_MONTHLY_eur"), (subscription.StripeCustomerId, subscription.StripePriceId));

        var rows = await QueryAsync(ctx => ctx.UserStripeCustomers.IgnoreQueryFilters()
            .Where(c => c.UserId == customerId)
            .Select(c => new { c.CurrencyId, c.StripeCustomerId })
            .ToListAsync());
        var eurRow = Assert.Single(rows);
        Assert.Equal((EurId, "cus_fake_1"), (eurRow.CurrencyId, eurRow.StripeCustomerId));

        var legacy = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().Where(u => u.Id == customerId).Select(u => u.StripeCustomerId).SingleAsync());
        Assert.Equal(LegacyCustomerId, legacy);

        var active = await QueryAsync(ctx => ctx.UserMemberships.IgnoreQueryFilters()
            .Where(m => m.UserId == customerId && m.Status == MembershipStatus.Active)
            .Select(m => new { m.CurrencyId, m.StripeSubscriptionId })
            .SingleAsync());
        Assert.Equal((EurId, "sub_fake_1"), (active.CurrencyId, active.StripeSubscriptionId));
    }

    [Fact]
    public async Task Subscribing_again_in_czk_adopts_the_legacy_customer_and_mints_nothing()
    {
        var customerId = await SeedAsync();
        var client = CustomerClient(TestJwtFactory.Mint(CustomerAudience, customerId, CustomerEmail, UserProfile.Customer));

        var subscribe = await client.PostAsJsonAsync(
            "/api/Membership/Subscribe",
            new { PlanCode = "PLUS_MONTHLY", PaymentMethodConfirmed = true, CountryId = CzeId, IdempotencyToken = "resub-czk-1" });

        HttpAssert.IsOk(subscribe);
        Assert.Equal(LegacyCustomerId, (await BodyAsync(subscribe)).GetProperty("stripeCustomerId").GetString());
        Assert.Empty(_stripe.CreatedCustomers);
        Assert.Equal(LegacyCustomerId, Assert.Single(_stripe.CreatedSubscriptions).StripeCustomerId);

        var row = await QueryAsync(ctx => ctx.UserStripeCustomers.IgnoreQueryFilters()
            .Where(c => c.UserId == customerId)
            .Select(c => new { c.CurrencyId, c.StripeCustomerId })
            .SingleAsync());
        Assert.Equal((DomainSeed.CurrencyId, LegacyCustomerId), (row.CurrencyId, row.StripeCustomerId));
    }
}
