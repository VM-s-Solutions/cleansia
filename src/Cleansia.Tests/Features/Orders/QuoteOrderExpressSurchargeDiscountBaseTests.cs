using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The wizard's itemised "Plus saving" and the one persisted on the order must be the same number.
/// <see cref="OrderFactory"/> discounts the RAW pre-surcharge subtotal and re-applies the express
/// surcharge on top; the quote used to discount the surcharge-inclusive gross. Both produce the same
/// final charge (multiplication commutes) but a different itemised discount — 120 shown, 100 stored —
/// and the stored one feeds the lifetime-savings stat, so the customer's receipt and their savings
/// total silently disagreed with the price they were quoted.
///
/// Numbers: base 1000, express slot (+20%) =&gt; gross 1200; Plus 10%.
/// Persisted path: (1000 - 100) * 1.2 = 1080, discount 100.
/// </summary>
public class QuoteOrderExpressSurchargeDiscountBaseTests
{
    private const string UserId = "user-plus-1";
    private const decimal BaseSubtotal = 1000m;
    private const decimal SurchargeAmount = 200m;
    private const decimal GrossSubtotal = BaseSubtotal + SurchargeAmount;
    private const decimal PlusPercentage = 10m;

    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();

    public QuoteOrderExpressSurchargeDiscountBaseTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeActiveMembership());
    }

    private static UserMembership ArrangeActiveMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: PlusPercentage,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);
        return membership;
    }

    private void ArrangePricing(bool expressSlot)
    {
        var surcharge = expressSlot ? SurchargeAmount : 0m;
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPricingResult(
                TotalPrice: BaseSubtotal + surcharge,
                CurrencyId: "currency-czk",
                CurrencyCode: "CZK",
                ServicesSubtotal: BaseSubtotal,
                PackagesSubtotal: 0m,
                ExtrasSubtotal: 0m,
                ExpressSurchargeApplied: expressSlot,
                ExpressSurchargeAmount: surcharge,
                ExchangeRate: 1m));
    }

    private QuoteOrder.Handler CreateHandler() =>
        new(
            _pricingCalculator.Object,
            _session.Object,
            _loyaltyService.Object,
            _tierConfigRepository.Object,
            _membershipRepository.Object);

    private static QuoteOrder.Command ExpressCommand() =>
        new([ "service-1" ], [], Rooms: 2, Bathrooms: 1, CurrencyId: null,
            SelectedExtraSlugs: null, CleaningDate: DateTime.UtcNow.AddHours(3));

    [Fact]
    public async Task ExpressSlot_PlusDiscountMatchesTheAmountTheOrderWillPersist()
    {
        ArrangePricing(expressSlot: true);

        var result = await CreateHandler().Handle(ExpressCommand(), CancellationToken.None);

        Assert.Equal(BaseSubtotal * PlusPercentage / 100m, result.Value.MembershipDiscountAmount);
    }

    [Fact]
    public async Task ExpressSlot_FinalPriceStillMatchesTheOrderFactoryTotal()
    {
        ArrangePricing(expressSlot: true);

        var result = await CreateHandler().Handle(ExpressCommand(), CancellationToken.None);

        var factoryTotal = (BaseSubtotal - BaseSubtotal * PlusPercentage / 100m)
            * (1 + BookingPolicy.ExpressSurchargeRate);
        Assert.Equal(factoryTotal, result.Value.FinalPriceAfterDiscount);
    }

    [Fact]
    public async Task ExpressSlot_TotalPriceStaysTheGrossTheClientMustResubmit()
    {
        ArrangePricing(expressSlot: true);

        var result = await CreateHandler().Handle(ExpressCommand(), CancellationToken.None);

        // CreateOrder.PriceMatchesAsync compares this against the calculator's surcharge-inclusive
        // total; changing it would reject every express booking with TotalPriceNotMatch.
        Assert.Equal(GrossSubtotal, result.Value.TotalPrice);
    }

    [Fact]
    public async Task ExpressSlot_OriginalSubtotalStaysConsistentWithTheDiscount()
    {
        ArrangePricing(expressSlot: true);

        var result = await CreateHandler().Handle(ExpressCommand(), CancellationToken.None);

        Assert.Equal(
            result.Value.FinalPriceAfterDiscount + result.Value.MembershipDiscountAmount,
            result.Value.OriginalSubtotal);
    }

    [Fact]
    public async Task ExpressSlot_TierDiscountIsResolvedOnTheRawSubtotalNotTheGross()
    {
        ArrangePricing(expressSlot: true);

        await CreateHandler().Handle(ExpressCommand(), CancellationToken.None);

        // The tier floor (min order amount) must be judged on the same base CreateOrder will use,
        // or a booking straddling the floor qualifies in the wizard and loses the discount at submit.
        _loyaltyService.Verify(
            s => s.ResolveTierDiscountForOrderAsync(UserId, BaseSubtotal, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoExpressSlot_IsUnchanged()
    {
        ArrangePricing(expressSlot: false);

        var result = await CreateHandler().Handle(
            new([ "service-1" ], [], Rooms: 2, Bathrooms: 1, CurrencyId: null,
                SelectedExtraSlugs: null, CleaningDate: DateTime.UtcNow.AddDays(3)),
            CancellationToken.None);

        Assert.Equal(BaseSubtotal, result.Value.TotalPrice);
        Assert.Equal(BaseSubtotal, result.Value.OriginalSubtotal);
        Assert.Equal(BaseSubtotal * PlusPercentage / 100m, result.Value.MembershipDiscountAmount);
        Assert.Equal(BaseSubtotal - BaseSubtotal * PlusPercentage / 100m, result.Value.FinalPriceAfterDiscount);
    }
}
