using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A Plus subscription is billed in one currency, but what it grants is a percentage, hours, a waiver
/// and a count — none of them money. A customer subscribed in CZK who quotes a Slovak address gets the
/// EUR discount on the EUR order, and no currency-mismatch guard exists to refuse it (ADR-0059 D2).
/// Pinned so nobody adds one by analogy with promo codes, whose minimum IS a money figure.
/// </summary>
public class QuoteOrderMembershipDiscountIsCurrencyFreeTests
{
    private const string UserId = "user-plus-czk";
    private const decimal EurSubtotal = 100m;
    private const decimal PlusPercentage = 10m;

    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();

    public QuoteOrderMembershipDiscountIsCurrencyFreeTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));

        var plan = MembershipPlan.Create("PLUS_MONTHLY", "Cleansia Plus", PlusPercentage, 4, true);
        var membership = UserMembership.Create(
            UserId, plan.Id, MembershipPricingMockFactory.CzkCurrencyId, "sub_czk",
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1));
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);
        _membershipRepository
            .Setup(r => r.GetEntitledForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPricingResult(
                TotalPrice: EurSubtotal,
                CurrencyId: MembershipPricingMockFactory.EurCurrencyId,
                CurrencyCode: "EUR",
                ServicesSubtotal: EurSubtotal,
                PackagesSubtotal: 0m,
                ExtrasSubtotal: 0m,
                ExpressSurchargeApplied: false,
                ExpressSurchargeAmount: 0m));
    }

    private QuoteOrder.Handler CreateHandler() =>
        new(
            _pricingCalculator.Object,
            _session.Object,
            _loyaltyService.Object,
            _membershipRepository.Object,
            _creditAccountRepository.Object,
            new Mock<ICurrencyResolutionService>().Object);

    [Fact]
    public async Task ACzkMembership_DiscountsAEurOrder_InEur_WithNoMismatchError()
    {
        var result = await CreateHandler().Handle(
            new QuoteOrder.Command(["service-1"], [], Rooms: 2, Bathrooms: 1,
                CurrencyId: MembershipPricingMockFactory.EurCurrencyId, SelectedExtraSlugs: null,
                CleaningDate: DateTime.UtcNow.AddDays(3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value.CurrencyCode);
        Assert.Equal(EurSubtotal * PlusPercentage / 100m, result.Value.MembershipDiscountAmount);
        Assert.Equal(EurSubtotal - EurSubtotal * PlusPercentage / 100m, result.Value.FinalPriceAfterDiscount);
    }
}
