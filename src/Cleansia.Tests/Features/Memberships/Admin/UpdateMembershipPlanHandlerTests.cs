using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// An update writes exactly the currencies it names: an existing row is updated in place, a new one is
/// added, and a currency the payload does not mention — active or not — is left exactly as it was.
/// That is what lets an admin change a benefit without minting a Stripe Price for every market.
/// </summary>
public class UpdateMembershipPlanHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly MembershipPlan _plan;

    public UpdateMembershipPlanHandlerTests()
    {
        _plan = MembershipPlan.Create("PLUS_MONTHLY", "Plus Monthly", 5m, 4, true);
        _plan.Id = "plan-1";
        _planRepository.Setup(r => r.GetByIdAsync(_plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _currencyRepository
            .Setup(r => r.GetAll())
            .Returns(new[] { MembershipPricingMockFactory.Czk(), MembershipPricingMockFactory.Eur() }.AsQueryable().BuildMock());
    }

    private UpdateMembershipPlan.Handler Handler() =>
        new(_planRepository.Object, _priceRepository.Object, _currencyRepository.Object);

    [Fact]
    public async Task ABenefitChangeSendingOnlyTheCzkRow_LeavesEurAbsent_AndUpdatesCzkInPlace()
    {
        var czk = _priceRepository.PriceIn(_plan.Id, MembershipPricingMockFactory.CzkCurrencyId, "price_czk", 199m);

        var result = await Handler().Handle(
            new UpdateMembershipPlan.Command(
                _plan.Id, "Plus Monthly",
                new Dictionary<string, MembershipPlanPriceInput> { ["CZK"] = new(249m, "price_czk_v2") },
                DiscountPercentage: 10m, FreeCancellationWindowHours: 4, TrialPeriodDays: 0, AllowsExpressUpgrade: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10m, _plan.DiscountPercentage);
        Assert.Equal(249m, czk.Price);
        Assert.Equal("price_czk_v2", czk.StripePriceId);
        _priceRepository.Verify(r => r.Add(It.IsAny<MembershipPlanPrice>()), Times.Never);
    }

    [Fact]
    public async Task ANewCurrency_GetsARow()
    {
        var added = new List<MembershipPlanPrice>();
        _priceRepository.Setup(r => r.Add(It.IsAny<MembershipPlanPrice>())).Callback<MembershipPlanPrice>(added.Add);

        await Handler().Handle(
            new UpdateMembershipPlan.Command(
                _plan.Id, "Plus Monthly",
                new Dictionary<string, MembershipPlanPriceInput> { ["EUR"] = new(7.99m, "price_eur") },
                DiscountPercentage: 5m, FreeCancellationWindowHours: 4, TrialPeriodDays: 0, AllowsExpressUpgrade: true),
            CancellationToken.None);

        var row = Assert.Single(added);
        Assert.Equal(MembershipPricingMockFactory.EurCurrencyId, row.CurrencyId);
        Assert.Equal(7.99m, row.Price);
        Assert.Equal("price_eur", row.StripePriceId);
    }
}
