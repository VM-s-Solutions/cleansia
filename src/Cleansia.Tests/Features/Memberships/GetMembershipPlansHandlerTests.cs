using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// The plan list answers in the resolved market's currency: only plans with a row in it, every figure
/// from that row, the savings badge from that currency's rows alone, and an empty list where no plan
/// is priced. Written before the handler took a market.
/// </summary>
public class GetMembershipPlansHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly MembershipPlan _monthly;
    private readonly MembershipPlan _yearly;

    public GetMembershipPlansHandlerTests()
    {
        _monthly = Plan("PLUS_MONTHLY", BillingInterval.Monthly);
        _yearly = Plan("PLUS_YEARLY", BillingInterval.Yearly);
        _planRepository
            .Setup(r => r.GetActivePlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_monthly, _yearly]);
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static MembershipPlan Plan(string code, BillingInterval interval)
    {
        var plan = MembershipPlan.Create(
            code: code, name: code, discountPercentage: 5m, freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true, billingInterval: interval);
        plan.Id = $"plan-{code}";
        return plan;
    }

    private void PricedIn(string currencyId, params (MembershipPlan Plan, decimal Price)[] rows)
    {
        _priceRepository
            .Setup(r => r.GetForPlansAsync(It.IsAny<IReadOnlyCollection<string>>(), currencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows.ToDictionary(
                r => r.Plan.Id,
                r => MembershipPlanPrice.Create(r.Plan.Id, currencyId, r.Price, $"price_{r.Plan.Code}_{currencyId}")));
    }

    private GetMembershipPlans.Handler Handler(Mock<Cleansia.Core.AppServices.Services.Interfaces.ICurrencyResolutionService> resolution) =>
        new(_planRepository.Object, _priceRepository.Object, resolution.Object, _countryRepository.Object);

    [Fact]
    public async Task AnUnservicedOrUnknownCountry_ListsNothing_AndNeverAsksTheResolver()
    {
        _countryRepository
            .Setup(r => r.IsServicedAsync("country-nowhere", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var resolution = new Mock<Cleansia.Core.AppServices.Services.Interfaces.ICurrencyResolutionService>(MockBehavior.Strict);

        var result = await Handler(resolution)
            .Handle(new GetMembershipPlans.Query("country-nowhere"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task AMarketWhereNoPlanIsPriced_ListsNothing()
    {
        PricedIn(MembershipPricingMockFactory.CzkCurrencyId, (_monthly, 199m), (_yearly, 2030m));
        PricedIn(MembershipPricingMockFactory.EurCurrencyId);

        var result = await Handler(MarketResolution.Resolving(MembershipPricingMockFactory.Eur()))
            .Handle(new GetMembershipPlans.Query("country-svk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task TheMarketsCurrency_LabelsEveryRow_AndTheSavingsComeFromThatCurrencysRowsOnly()
    {
        PricedIn(MembershipPricingMockFactory.CzkCurrencyId, (_monthly, 199m), (_yearly, 2030m));
        PricedIn(MembershipPricingMockFactory.EurCurrencyId, (_monthly, 7.99m), (_yearly, 59.88m));

        var czk = (await Handler(MarketResolution.Resolving())
            .Handle(new GetMembershipPlans.Query("country-cze"), CancellationToken.None)).Value;
        var eur = (await Handler(MarketResolution.Resolving(MembershipPricingMockFactory.Eur()))
            .Handle(new GetMembershipPlans.Query("country-svk"), CancellationToken.None)).Value;

        Assert.All(czk, p => Assert.Equal("CZK", p.CurrencyCode));
        Assert.All(eur, p => Assert.Equal("EUR", p.CurrencyCode));

        var czkYearly = czk.Single(p => p.Code == "PLUS_YEARLY");
        Assert.Equal(2030m, czkYearly.Price);
        Assert.Equal(169.17m, czkYearly.MonthlyEquivalentPrice);
        Assert.Equal(Math.Round((1m - 169.17m / 199m) * 100m, 0), czkYearly.SavingsPercentVsMonthly);

        var eurYearly = eur.Single(p => p.Code == "PLUS_YEARLY");
        Assert.Equal(59.88m, eurYearly.Price);
        Assert.Equal(4.99m, eurYearly.MonthlyEquivalentPrice);
        Assert.Equal(Math.Round((1m - 4.99m / 7.99m) * 100m, 0), eurYearly.SavingsPercentVsMonthly);
    }

    [Fact]
    public async Task APlanPricedOnlyElsewhere_IsAbsent_NotZero()
    {
        PricedIn(MembershipPricingMockFactory.CzkCurrencyId, (_monthly, 199m));

        var result = await Handler(MarketResolution.Resolving())
            .Handle(new GetMembershipPlans.Query(null), CancellationToken.None);

        var only = Assert.Single(result.Value);
        Assert.Equal("PLUS_MONTHLY", only.Code);
        Assert.Equal(0m, only.SavingsPercentVsMonthly);
    }
}
