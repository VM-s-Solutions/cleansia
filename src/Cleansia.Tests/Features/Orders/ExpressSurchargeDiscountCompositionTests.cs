using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The discount and the express surcharge COMPOSE, and every per-term test passes while the
/// composition is wrong — which is how this survived.
///
/// <para><see cref="OrderFactory"/> resolves the discount against the RAW pre-surcharge subtotal and
/// then grosses the remainder up by the surcharge: <c>(raw - d) * 1.2</c>. Resolving on the raw base
/// is deliberate and stays — the tier floor and the LOY-003 cap must be judged on the same base the
/// quote judged them on. But the amount that was REPORTED and PERSISTED was <c>d</c>, the raw-base
/// figure, while the price it is subtracted from carries the surcharge. Every consumer composes the
/// two — the mappers' <c>OriginalSubtotal = TotalPrice + applied</c>, the lifetime-savings sum, and
/// all three clients' <c>totalPrice - discount</c> — so every one of them was wrong by exactly
/// <c>ExpressSurchargeRate * d</c>.</para>
///
/// <para>The customer's real saving on an express order is <c>d * 1.2</c>: they would have paid
/// <c>raw * 1.2</c> and they pay <c>(raw - d) * 1.2</c>. Reporting <c>d</c> under-states it by a fifth.
/// So the reported amounts are grossed by the same factor as the price, and the charged total does not
/// move by a cent.</para>
/// </summary>
public class ExpressSurchargeDiscountCompositionTests
{
    private const string UserId = "user-1";
    private const decimal RawSubtotal = 1000m;
    private const decimal GoldTierAmount = 100m;
    private const decimal PlusPercentage = 5m;

    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();

    public ExpressSurchargeDiscountCompositionTests()
    {
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
    }

    /// <summary>
    /// The flagship case, in whole korunas so a wrong constant is visible: Gold (10%) + Plus (5%) on a
    /// 1000 Kč express basket. LOY-003 caps the pair at 12% of raw = 120, pro-rated to 40 Plus / 80
    /// tier; the charge is 880 * 1.2 = 1056 against an undiscounted 1200, so the saving is 144.
    /// </summary>
    [Fact]
    public async Task Express_TierAndPlus_ReportTheSavingTheCustomerActuallyGets()
    {
        var order = await CreateOrderAsync(express: true, tierDiscount: GoldTierAmount, hasPlus: true);

        Assert.Equal(1056m, order.TotalPrice);
        Assert.Equal(48m, order.MembershipDiscountAmount);
        Assert.Equal(96m, order.TierDiscountAmount);
        Assert.Equal(144m, 1200m - order.TotalPrice);
    }

    /// <summary>
    /// The invariant every consumer relies on, asserted on EVERY branch: what was charged plus what was
    /// reported as saved reconstructs the price the customer would have paid with no discount at all.
    /// <c>OrderMappers.ResolveAppliedDiscount</c> derives the receipt's struck-through
    /// <c>OriginalSubtotal</c> as exactly this sum, and the order carries no express flag for it to
    /// correct with, so the correction can only live at write time.
    /// </summary>
    [Theory]
    // tier-only, Plus-only, both under the cap, both over the cap (pro-rated), promo wins, no discount
    [InlineData(true, 100, false, 0)]
    [InlineData(true, 0, true, 0)]
    [InlineData(true, 50, true, 0)]
    [InlineData(true, 100, true, 0)]
    [InlineData(true, 100, true, 200)]
    [InlineData(true, 0, false, 0)]
    [InlineData(false, 100, true, 0)]
    [InlineData(false, 100, true, 200)]
    public async Task ChargedTotalPlusReportedDiscountIsTheUndiscountedPrice(
        bool express, int tierDiscount, bool hasPlus, int promoDiscount)
    {
        var order = await CreateOrderAsync(express, tierDiscount, hasPlus, promoDiscount);

        var reported = (order.TierDiscountAmount ?? 0m)
            + (order.MembershipDiscountAmount ?? 0m)
            + (order.PromoDiscountAmount ?? 0m);

        Assert.Equal(
            BookingPolicy.ApplyExpressSurcharge(RawSubtotal, express),
            order.TotalPrice + reported);
    }

    /// <summary>
    /// The charged price is not what moved. Pinned separately from the reconciliation above, which a
    /// change to the surcharge rate would satisfy on both sides at once.
    /// </summary>
    [Fact]
    public async Task Express_TheChargedTotalIsUnchangedByHowTheDiscountIsReported()
    {
        var withPromo = await CreateOrderAsync(express: true, tierDiscount: GoldTierAmount, hasPlus: true, promoDiscount: 200m);
        var withoutPromo = await CreateOrderAsync(express: true, tierDiscount: GoldTierAmount, hasPlus: true);

        Assert.Equal(960m, withPromo.TotalPrice);
        Assert.Equal(1056m, withoutPromo.TotalPrice);
    }

    /// <summary>
    /// A winning promo replaces the pair (ADR-0038 §D5.1) and must be grossed the same way — otherwise
    /// the one branch where a customer entered a code is the one branch whose receipt is wrong.
    /// </summary>
    [Fact]
    public async Task Express_AWinningPromoIsReportedAgainstTheChargedPriceToo()
    {
        var order = await CreateOrderAsync(
            express: true, tierDiscount: GoldTierAmount, hasPlus: true, promoDiscount: 200m);

        Assert.Equal(240m, order.PromoDiscountAmount);
        Assert.Null(order.TierDiscountAmount);
        Assert.Null(order.MembershipDiscountAmount);
    }

    /// <summary>
    /// The wizard's itemised saving and the one the order persists are the same number — the property
    /// the quote/factory alignment was always about, now held at the corrected value on both sides.
    /// </summary>
    [Fact]
    public async Task Express_TheQuoteAndTheOrderReportTheSameDiscount()
    {
        var quote = await QuoteAsync(express: true, tierDiscount: GoldTierAmount, plusPercentage: PlusPercentage);
        var order = await CreateOrderAsync(express: true, tierDiscount: GoldTierAmount, hasPlus: true);

        Assert.Equal(order.MembershipDiscountAmount, quote.MembershipDiscountAmount);
        Assert.Equal(order.TierDiscountAmount, quote.TierDiscountAmount);
        Assert.Equal(order.TotalPrice, quote.FinalPriceAfterDiscount);
    }

    /// <summary>
    /// The quote's two "before discount" fields are one number. <c>TotalPrice</c> is the gross the client
    /// resubmits to <c>CreateOrder.PriceMatchesAsync</c>; <c>OriginalSubtotal</c> is what renders struck
    /// through next to it. They disagreed by a fifth of the discount, in the same response.
    /// </summary>
    [Fact]
    public async Task Express_TheQuotesTwoUndiscountedFieldsAgree()
    {
        var quote = await QuoteAsync(express: true, tierDiscount: GoldTierAmount, plusPercentage: PlusPercentage);

        Assert.Equal(quote.TotalPrice, quote.OriginalSubtotal);
    }

    private async Task<Order> CreateOrderAsync(
        bool express, decimal tierDiscount, bool hasPlus, decimal promoDiscount = 0m)
    {
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(tierDiscount, LoyaltyTier.GoldPolisher));
        _userMembershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPlus ? ActiveMembership(PlusPercentage) : null);

        var factory = new OrderFactory(
            _orderRepository.Object,
            _serviceRepository.Object,
            _packageRepository.Object,
            PayConfigRepositoryDouble.Holding(),
            _companyInfoRepository.Object,
            _countryConfigurationRepository.Object,
            _vatCalculator.Object,
            _loyaltyService.Object,
            _userMembershipRepository.Object,
            NoPreferredCleanerHold.Resolver,
            _notificationProducer.Object);

        return await factory.CreateAsync(
            new CreateOrderInput(
                UserId: UserId,
                CustomerName: "Test Customer",
                CustomerEmail: "customer@example.com",
                CustomerPhone: "+420123456789",
                Address: AddressMockFactory.Generate(),
                Rooms: 2,
                Bathrooms: 1,
                Extras: new Dictionary<string, bool>(),
                CleaningDate: express ? Now.AddHours(3) : Now.AddDays(3),
                PaymentType: PaymentType.Cash,
                Currency: Currency.Create("CZK", "Kč", "Czech Koruna", 1m),
                SelectedServiceIds: ["service-1"],
                SelectedPackageIds: [],
                RawSubtotal: RawSubtotal,
                NowUtc: Now,
                ReservedExpressWaiver: null,
                PromoDiscountAmount: promoDiscount),
            CancellationToken.None);
    }

    private static async Task<QuoteOrder.Response> QuoteAsync(
        bool express, decimal tierDiscount, decimal plusPercentage)
    {
        var surcharge = express ? RawSubtotal * BookingPolicy.ExpressSurchargeRate : 0m;
        var pricingCalculator = new Mock<IOrderPricingCalculator>();
        pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPricingResult(
                TotalPrice: RawSubtotal + surcharge,
                CurrencyId: "currency-czk",
                CurrencyCode: "CZK",
                ServicesSubtotal: RawSubtotal,
                PackagesSubtotal: 0m,
                ExtrasSubtotal: 0m,
                ExpressSurchargeApplied: express,
                ExpressSurchargeAmount: surcharge,
                ExchangeRate: 1m));

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(UserId);

        var loyaltyService = new Mock<ILoyaltyService>();
        loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(tierDiscount, LoyaltyTier.GoldPolisher));

        var membershipRepository = new Mock<IUserMembershipRepository>();
        membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMembership(plusPercentage));

        var handler = new QuoteOrder.Handler(
            pricingCalculator.Object,
            session.Object,
            loyaltyService.Object,
            new Mock<ILoyaltyTierConfigRepository>().Object,
            membershipRepository.Object);

        var result = await handler.Handle(
            new QuoteOrder.Command(
                ["service-1"], [], Rooms: 2, Bathrooms: 1, CurrencyId: null,
                SelectedExtraSlugs: null, CleaningDate: Now.AddHours(3)),
            CancellationToken.None);

        return result.Value;
    }

    private static UserMembership ActiveMembership(decimal discountPercentage)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: discountPercentage,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: Now.AddDays(-1),
            currentPeriodEnd: Now.AddMonths(1));
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);
        return membership;
    }
}
