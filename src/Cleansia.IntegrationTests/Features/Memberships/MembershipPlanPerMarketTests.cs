using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// Cleansia Plus is priced per market (ADR-0059) over real Postgres: the customer plan list answers in
/// the market's currency and lists only the plans priced in it; the price row's two uniques and the
/// currency FK are what the migration actually emits; and a currency with a plan price or a
/// subscription in it refuses to be deleted with <c>currency.in_use</c> rather than a raw 23503.
/// </summary>
[Collection("PostgresCollection")]
public class MembershipPlanPerMarketTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzkId = "currency-czk-plusmarket";
    private const string EurId = "currency-eur-plusmarket";
    private const string CzeId = "country-cze-plusmarket";
    private const string SvkId = "country-svk-plusmarket";
    private const string MonthlyId = "plan-monthly-plusmarket";
    private const string YearlyId = "plan-yearly-plusmarket";

    [Fact]
    public async Task A_Market_With_No_Priced_Plan_Lists_Nothing_And_The_Priced_Market_Lists_Both_In_Its_Currency()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var svk = await mediator.Send(new GetMembershipPlans.Query(SvkId));
                var cze = await mediator.Send(new GetMembershipPlans.Query(CzeId));
                var defaultMarket = await mediator.Send(new GetMembershipPlans.Query(null));
                return (svk, cze, defaultMarket);
            },
            assert: (CleansiaDbContext _, (BusinessResult<IReadOnlyList<GetMembershipPlans.Response>> svk,
                BusinessResult<IReadOnlyList<GetMembershipPlans.Response>> cze,
                BusinessResult<IReadOnlyList<GetMembershipPlans.Response>> defaultMarket) r) =>
            {
                Assert.True(r.svk.IsSuccess);
                Assert.Empty(r.svk.Value);

                Assert.True(r.cze.IsSuccess);
                Assert.Equal(2, r.cze.Value.Count);
                Assert.All(r.cze.Value, p => Assert.Equal("CZK", p.CurrencyCode));
                var yearly = r.cze.Value.Single(p => p.Code == "PLUS_YEARLY");
                Assert.Equal(2030m, yearly.Price);
                Assert.Equal(169.17m, yearly.MonthlyEquivalentPrice);
                Assert.Equal(Math.Round((1m - 169.17m / 199m) * 100m, 0), yearly.SavingsPercentVsMonthly);

                Assert.Equal(r.cze.Value.Select(p => p.Code), r.defaultMarket.Value.Select(p => p.Code));
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Schema_Refuses_A_Second_Row_Per_Plan_And_Currency_And_A_Shared_Stripe_Price()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx),
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();

                ctx.MembershipPlanPrices.Add(MembershipPlanPrice.Create(MonthlyId, CzkId, 1m, "price_second_row"));
                var duplicatePair = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                ctx.ChangeTracker.Clear();

                ctx.MembershipPlanPrices.Add(MembershipPlanPrice.Create(MonthlyId, EurId, 7.99m, "price_monthly_czk"));
                var sharedStripeId = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                ctx.ChangeTracker.Clear();

                return (
                    Pair: (duplicatePair.InnerException as PostgresException)?.ConstraintName,
                    Stripe: (sharedStripeId.InnerException as PostgresException)?.ConstraintName);
            },
            assert: (CleansiaDbContext _, (string? Pair, string? Stripe) r) =>
            {
                Assert.Equal("IX_MembershipPlanPrices_MembershipPlanId_CurrencyId", r.Pair);
                Assert.Equal("IX_MembershipPlanPrices_StripePriceId", r.Stripe);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task Deleting_A_Plan_Takes_Its_Price_Rows_With_It()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx),
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                await ctx.MembershipPlans.Where(p => p.Id == YearlyId).ExecuteDeleteAsync();
                return await ctx.MembershipPlanPrices.CountAsync(p => p.MembershipPlanId == YearlyId);
            },
            assert: (CleansiaDbContext _, int remaining) =>
            {
                Assert.Equal(0, remaining);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Currency_With_A_Plan_Price_Or_A_Subscription_In_It_Answers_In_Use()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                await SeedAsync(ctx, priceYearlyInEur: true);

                var user = User.CreateWithPassword("plus-market@cleansia.test", "12345678Test!", "Plus", "Member");
                user.ConfirmEmail();
                ctx.Users.Add(user);
                var gbp = Currency.Create("GBP", "£", "Pound sterling");
                gbp.Id = "currency-gbp-plusmarket";
                ctx.Currencies.Add(gbp);
                await ctx.CommitAsync(CancellationToken.None);

                // A subscription billed in GBP, with no GBP price row: the membership alone must guard it.
                ctx.UserMemberships.Add(UserMembership.Create(
                    user.Id, MonthlyId, gbp.Id, "sub_gbp", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var eur = await mediator.Send(new DeleteCurrency.Command(EurId));
                var gbp = await mediator.Send(new DeleteCurrency.Command("currency-gbp-plusmarket"));
                return (eur, gbp);
            },
            assert: async (CleansiaDbContext ctx, (BusinessResult<DeleteCurrency.Response> eur, BusinessResult<DeleteCurrency.Response> gbp) r) =>
            {
                Assert.True(r.eur.IsFailure);
                Assert.Contains(Assert.IsAssignableFrom<IValidationResult>(r.eur).Errors, e => e.Message == BusinessErrorMessage.CurrencyInUse);
                Assert.True(r.gbp.IsFailure);
                Assert.Contains(Assert.IsAssignableFrom<IValidationResult>(r.gbp).Errors, e => e.Message == BusinessErrorMessage.CurrencyInUse);
                Assert.Equal(2, await ctx.Currencies.CountAsync(c => c.Id == EurId || c.Id == "currency-gbp-plusmarket"));
            });
    }

    private static async Task SeedAsync(CleansiaDbContext ctx, bool priceYearlyInEur = false)
    {
        ctx.Languages.Add(Language.Create("en", "English"));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.Id = CzkId;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.IsActive = true;
        eur.Id = EurId;
        ctx.Currencies.AddRange(czk, eur);

        var cze = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        cze.Id = CzeId;
        var svk = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        svk.Id = SvkId;
        ctx.Countries.AddRange(cze, svk);
        ctx.CountryConfigurations.Add(CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m));
        ctx.CountryConfigurations.Add(CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m));

        var monthly = MembershipPlan.Create("PLUS_MONTHLY", "Cleansia Plus (Monthly)", 5m, 4, true);
        monthly.Id = MonthlyId;
        var yearly = MembershipPlan.Create("PLUS_YEARLY", "Cleansia Plus (Annual)", 5m, 4, true, BillingInterval.Yearly);
        yearly.Id = YearlyId;
        ctx.MembershipPlans.AddRange(monthly, yearly);

        ctx.MembershipPlanPrices.Add(MembershipPlanPrice.Create(MonthlyId, CzkId, 199m, "price_monthly_czk"));
        ctx.MembershipPlanPrices.Add(MembershipPlanPrice.Create(YearlyId, CzkId, 2030m, "price_yearly_czk"));
        if (priceYearlyInEur)
        {
            ctx.MembershipPlanPrices.Add(MembershipPlanPrice.Create(YearlyId, EurId, 59.88m, "price_yearly_eur"));
        }

        await ctx.CommitAsync(CancellationToken.None);
    }
}
