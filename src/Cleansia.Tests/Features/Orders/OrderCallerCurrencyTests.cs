using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// THE QUOTE IS PRICED IN THE CURRENCY OF THE COUNTRY THE SERVICE ADDRESS IS IN (owner ruling
/// 2026-09-12): the caller sends the address's <c>CountryId</c> once the wizard has one, and the
/// server resolves the currency — the caller never picks one. An explicit <c>CurrencyId</c> still
/// wins, so a client that echoes the currency it was quoted in keeps agreeing with itself; with
/// neither field the quote is in the platform default, which is what every wizard opens with.
///
/// <para>Whatever the resolution, the currency must be one the platform can quote in —
/// <c>ICurrencyRepository.IsOfferableAsync</c>, switched on AND priced — and every selected entry
/// must have a price row in it, or the calculator would throw on the entry and the customer would see
/// a 500 for a service they were shown in another market.</para>
///
/// <para>Both quote surfaces are pinned, and the pass-through is asserted on the calculator's
/// argument rather than on the response, because the response echoed a currency code before this
/// change too. What changed is what the calculator is TOLD.</para>
/// </summary>
public class OrderCallerCurrencyTests
{
    private const string Eur = "currency-eur";
    private const string Huf = "currency-huf";
    private const string Czechia = "country-cze";
    private const string Slovakia = "country-svk";
    private const string Hungary = "country-hun";
    private const string Argentina = "country-arg";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency EurCurrency = WithId(Currency.Create("EUR", "€", "Euro"), Eur);
    private static readonly Currency HufCurrency = WithId(Currency.Create("HUF", "Ft", "Forint"), Huf);

    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<IMembershipPlanRepository> _membershipPlanRepository = new();
    private readonly ICurrencyResolutionService _markets = OrderMarketDoubles.Trading(
        Czk, (Czechia, Czk), (Slovakia, EurCurrency), (Hungary, HufCurrency));
    private readonly ICountryRepository _countries = OrderMarketDoubles.Servicing(Czechia, Slovakia, Hungary);

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
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(Czk.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Czk);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
    }

    private static Currency WithId(Currency currency, string id)
    {
        currency.Id = id;
        currency.IsActive = true;
        return currency;
    }

    /// <summary>The selection is priced in CZK and EUR, not in HUF — the shape of a market with no rows yet.</summary>
    private QuoteOrder.Validator QuoteValidator() =>
        new(
            _serviceRepository.Object,
            _packageRepository.Object,
            _currencyRepository.Object,
            _countries,
            _markets,
            CataloguePriceDoubles.Services(Czk, ("service-1", 500m, 100m), ("service-eur-priced", 20m, 4m)),
            CataloguePriceDoubles.Packages(Czk, ("package-1", 1000m)));

    private QuoteOrder.Handler QuoteHandler() =>
        new(
            _pricingCalculator.Object,
            _session.Object,
            _loyaltyService.Object,
            _membershipRepository.Object,
            _creditAccountRepository.Object,
            _markets);

    private static QuoteOrder.Command Quote(string? currencyId, string? countryId = null) =>
        new(["service-1"], [], Rooms: 2, Bathrooms: 1, CurrencyId: currencyId, CountryId: countryId);

    private void VerifyCalculatorTold(string? currencyId, Times times) =>
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
            currencyId, It.IsAny<DateTime?>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), times);

    // ---------------------------------------------------------------- QuoteOrder: the resolution

    [Fact]
    public async Task Quote_Passes_An_Offerable_Currency_To_The_Calculator()
    {
        await QuoteHandler().Handle(Quote(Eur), CancellationToken.None);

        VerifyCalculatorTold(Eur, Times.Once());
    }

    [Fact]
    public async Task Quote_With_Neither_Currency_Nor_Country_Is_The_Platform_Default()
    {
        await QuoteHandler().Handle(Quote(null), CancellationToken.None);

        VerifyCalculatorTold(null, Times.Once());
    }

    /// <summary>
    /// THE RULING. A Slovak address with no currency named is quoted in EUR — the country's currency,
    /// looked up server-side — not in the platform default and not in anything the client chose.
    /// </summary>
    [Fact]
    public async Task Quote_Derives_The_Currency_From_The_Country_When_None_Is_Named()
    {
        await QuoteHandler().Handle(Quote(null, Slovakia), CancellationToken.None);

        VerifyCalculatorTold(Eur, Times.Once());
    }

    [Fact]
    public async Task Quote_Named_Currency_Wins_Over_Country()
    {
        await QuoteHandler().Handle(Quote(CreateOrderTestData.CurrencyId, Slovakia), CancellationToken.None);

        VerifyCalculatorTold(CreateOrderTestData.CurrencyId, Times.Once());
    }

    // ---------------------------------------------------------------- QuoteOrder: the rules

    [Fact]
    public async Task Quote_Refuses_A_Currency_The_Platform_Cannot_Quote_In()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(Huf));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        Assert.Equal(nameof(QuoteOrder.Command.CurrencyId), failure.ErrorCode);
    }

    /// <summary>
    /// The offerable rule runs on the RESOLVED currency: a country configured for a currency that is
    /// not yet operated is refused with the currency key, before the calculator is reached.
    /// </summary>
    [Fact]
    public async Task Quote_Refuses_A_Country_Whose_Currency_The_Platform_Cannot_Quote_In()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(null, Hungary));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        _currencyRepository.Verify(r => r.IsOfferableAsync(Huf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Quote_Refuses_A_Country_The_Platform_Does_Not_Service()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(null, Argentina));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(nameof(QuoteOrder.Command.CountryId), failure.ErrorCode);
    }

    [Fact]
    public async Task Quote_Accepts_An_Offerable_Currency()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(Eur));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    /// <summary>Null is the default and needs no lookup — the rule is gated on a currency being resolved.</summary>
    [Fact]
    public async Task Quote_With_No_Currency_And_No_Country_Consults_Nothing()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(null));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        _currencyRepository.Verify(
            r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- QuoteOrder: every entry priced

    /// <summary>
    /// A service the customer picked from the CZK catalogue, then a Slovak address: the service has no
    /// EUR row, so the quote is refused with the selection key — a 400 the wizard renders — rather than
    /// reaching the calculator, which throws on exactly this and would surface as a 500.
    /// </summary>
    [Fact]
    public async Task Quote_Refuses_A_Service_With_No_Price_Row_In_The_Quote_Currency()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(null, Slovakia));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(QuoteOrder.Command.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
    }

    [Fact]
    public async Task Quote_Refuses_A_Package_With_No_Price_Row_In_The_Quote_Currency()
    {
        var command = new QuoteOrder.Command(
            [], ["package-1"], Rooms: 2, Bathrooms: 1, CurrencyId: null, CountryId: Slovakia);

        var result = await QuoteValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(QuoteOrder.Command.SelectedPackageIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedPackage);
    }

    /// <summary>Anti-vacuity: the same selection in the market it IS priced in passes the same rule.</summary>
    [Fact]
    public async Task Quote_Accepts_A_Selection_Priced_In_The_Quote_Currency()
    {
        var result = await QuoteValidator().ValidateAsync(Quote(null, Czechia));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    // ---------------------------------------------------------------- QuotePlusSavings

    private QuotePlusSavings.Handler PlusHandler() =>
        new(_pricingCalculator.Object, _membershipPlanRepository.Object, _loyaltyService.Object, _session.Object, _markets);

    private QuotePlusSavings.Validator PlusValidator() =>
        new(
            _serviceRepository.Object,
            _packageRepository.Object,
            _currencyRepository.Object,
            _countries,
            _markets,
            CataloguePriceDoubles.Services(Czk, ("service-1", 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk, ("package-1", 1000m)));

    private static QuotePlusSavings.Query PlusQuery(string? currencyId, string? countryId = null) =>
        new(["service-1"], [], Rooms: 2, Bathrooms: 1, PlanCode: "plus-monthly",
            CurrencyId: currencyId, CountryId: countryId);

    private void ArrangePlusPlan()
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
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));
    }

    /// <summary>
    /// The third caller-currency surface. Leaving it on the default while the other two follow the
    /// address would quote a Plus saving in a different currency from the price it sits under.
    /// </summary>
    [Fact]
    public async Task Plus_Savings_Passes_The_Callers_Currency_To_The_Calculator()
    {
        ArrangePlusPlan();

        await PlusHandler().Handle(PlusQuery(Eur), CancellationToken.None);

        VerifyCalculatorTold(Eur, Times.AtLeastOnce());
    }

    [Fact]
    public async Task Plus_Savings_Derives_The_Currency_From_The_Country_When_None_Is_Named()
    {
        ArrangePlusPlan();

        await PlusHandler().Handle(PlusQuery(null, Slovakia), CancellationToken.None);

        VerifyCalculatorTold(Eur, Times.AtLeastOnce());
    }

    [Fact]
    public async Task Plus_Savings_Refuses_A_Currency_The_Platform_Cannot_Quote_In()
    {
        var result = await PlusValidator().ValidateAsync(PlusQuery(Huf));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    [Fact]
    public async Task Plus_Savings_Refuses_A_Country_Whose_Currency_The_Platform_Cannot_Quote_In()
    {
        var result = await PlusValidator().ValidateAsync(PlusQuery(null, Hungary));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    [Fact]
    public async Task Plus_Savings_Refuses_A_Country_The_Platform_Does_Not_Service()
    {
        var result = await PlusValidator().ValidateAsync(PlusQuery(null, Argentina));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CountryNotServiced);
    }
}
