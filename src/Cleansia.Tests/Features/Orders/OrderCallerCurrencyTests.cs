using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// THE CALLER NAMES THE CURRENCY ON THE QUOTE, AND THE QUOTE HONOURS IT — provided the platform can
/// quote in it. Before this, every quote and every order took the platform default and the wire field
/// was accepted and ignored, so two currencies could never be live at once: the only "switch" was
/// promoting a new default, which swapped the whole platform.
///
/// <para>The guard is <c>ICurrencyRepository.IsOfferableAsync</c> — switched on AND priced. "Exists"
/// was the pre-Wave-A rule and was the hole: every seeded currency existed. Honouring the field is
/// safe now because nothing converts; a currency selects which price ROWS are read.</para>
///
/// <para>Both quote surfaces are pinned, and the pass-through is asserted on the calculator's
/// argument rather than on the response, because the response echoed a currency code before this
/// change too — from the default. What changed is what the calculator is TOLD.</para>
/// </summary>
public class OrderCallerCurrencyTests
{
    private const string Eur = "currency-eur";
    private const string Huf = "currency-huf";

    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<IMembershipPlanRepository> _membershipPlanRepository = new();

    public OrderCallerCurrencyTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // The span-cap rule and the handler both read the catalogue through GetByIds; an async-capable
        // empty query keeps them off the subject of this suite.
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(Eur, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(Huf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
    }

    private QuoteOrder.Validator QuoteValidator() =>
        new(_serviceRepository.Object, _packageRepository.Object, _currencyRepository.Object);

    private QuoteOrder.Handler QuoteHandler() =>
        new(
            _pricingCalculator.Object,
            _serviceRepository.Object,
            _packageRepository.Object,
            _session.Object,
            _loyaltyService.Object,
            _tierConfigRepository.Object,
            _membershipRepository.Object,
            _creditAccountRepository.Object);

    private static QuoteOrder.Command Quote(string? currencyId) =>
        new(["service-1"], [], Rooms: 2, Bathrooms: 1, CurrencyId: currencyId);

    private void VerifyCalculatorTold(string? currencyId, Times times) =>
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
            currencyId, It.IsAny<DateTime?>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), times);

    // ---------------------------------------------------------------- QuoteOrder

    [Fact]
    public async Task Quote_Passes_An_Offerable_Currency_To_The_Calculator()
    {
        await QuoteHandler().Handle(Quote(Eur), CancellationToken.None);

        VerifyCalculatorTold(Eur, Times.Once());
    }

    [Fact]
    public async Task Quote_Passes_Null_Through_As_The_Platform_Default()
    {
        await QuoteHandler().Handle(Quote(null), CancellationToken.None);

        VerifyCalculatorTold(null, Times.Once());
    }

    [Fact]
    public async Task Quote_Refuses_A_Currency_The_Platform_Cannot_Quote_In()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(Huf));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    [Fact]
    public async Task Quote_Accepts_An_Offerable_Currency()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(Eur));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    /// <summary>Null is the default and needs no lookup — the rule is gated on the field being set.</summary>
    [Fact]
    public async Task Quote_With_No_Currency_Consults_Nothing()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(null));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        _currencyRepository.Verify(
            r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- QuotePlusSavings

    private QuotePlusSavings.Handler PlusHandler() =>
        new(_pricingCalculator.Object, _membershipPlanRepository.Object, _loyaltyService.Object, _session.Object);

    private static QuotePlusSavings.Query PlusQuery(string? currencyId) =>
        new(["service-1"], [], Rooms: 2, Bathrooms: 1, PlanCode: "plus-monthly", CurrencyId: currencyId);

    /// <summary>
    /// The third caller-currency surface. Leaving it on null while the other two honour the caller
    /// would quote a Plus saving in a different currency from the price it sits under.
    /// </summary>
    [Fact]
    public async Task Plus_Savings_Passes_The_Callers_Currency_To_The_Calculator()
    {
        // The handler refuses before pricing when the plan is missing, so a real active plan is the
        // arrangement that lets the calculator be reached at all.
        _membershipPlanRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MembershipPlan.Create(
                code: "PLUS_MONTHLY", name: "Cleansia Plus", monthlyPriceCzk: 199m,
                stripePriceId: "price_test", discountPercentage: 10m, freeCancellationWindowHours: 4,
                allowsExpressUpgrade: true, billingInterval: BillingInterval.Monthly,
                trialPeriodDays: 0, expressUpgradesPerMonth: 1));
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));

        await PlusHandler().Handle(PlusQuery(Eur), CancellationToken.None);

        VerifyCalculatorTold(Eur, Times.AtLeastOnce());
    }

    [Fact]
    public async Task Plus_Savings_Refuses_A_Currency_The_Platform_Cannot_Quote_In()
    {
        var result = await new QuotePlusSavings.Validator(_currencyRepository.Object).ValidateAsync(PlusQuery(Huf));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }
}
