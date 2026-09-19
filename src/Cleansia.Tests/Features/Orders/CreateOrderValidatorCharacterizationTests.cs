using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;
using Cleansia.Core.AppServices.Tenancy;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Characterization of <c>CreateOrder.Validator</c> as it stands after the Wave-0 F2 change and before
/// the AUD-06 decomposition. Pins the observable validation contract — the happy pass and every rule's
/// <see cref="BusinessErrorMessage"/> code — so the future handler split can be proven behavior-preserving.
/// </summary>
public class CreateOrderValidatorCharacterizationTests
{
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPromoCodeService> _promoCodeService = new();

    private const string Slovakia = "sk";
    private const string Hungary = "hu";
    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency Eur = Market(Currency.Create("EUR", "€", "Euro"), "currency-eur");
    private static readonly Currency Huf = Market(Currency.Create("HUF", "Ft", "Forint"), "currency-huf");

    private static Currency Market(Currency currency, string id)
    {
        currency.Id = id;
        currency.IsActive = true;
        return currency;
    }

    public CreateOrderValidatorCharacterizationTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // The span-cap rule reads the catalog's durations; an empty catalog is 0 minutes, which keeps
        // every case here on the side of the cap it was written for. OrderSpanCapTests owns the bound.
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
    }

    /// <summary>
    /// The address country decides the currency: the fixture's "cz" trades in CZK, Slovakia in EUR,
    /// Hungary in HUF; the selection is priced in CZK and EUR only, so a Hungarian address is a market
    /// with no rows. Prices are keyed by currency id and the doubles hold one row per (entry, currency).
    /// </summary>
    private CreateOrder.Validator CreateValidator(
        IServicePriceRepository? servicePrices = null,
        IPackagePriceRepository? packagePrices = null) =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _userMembershipRepository.Object,
            _session.Object,
            PayConfigRepositoryDouble.Holding(),
            _currencyRepository.Object,
            OrderMarketDoubles.AddressAsGiven(),
            OrderMarketDoubles.Trading(Czk, ("cz", Czk), (Slovakia, Eur), (Hungary, Huf)),
            servicePrices ?? PricedServices(Czk, Eur),
            packagePrices ?? PricedPackages(Czk, Eur),
            _promoCodeService.Object,
            Cleansia.Tests.Features.Orders.OrderMarketDoubles.OperatedBy("cleansia-cz"),
            Cleansia.Tests.Features.Orders.OrderMarketDoubles.TenantAt("cleansia-cz"),
            Mock.Of<IUserConsentRepository>(),
            CreateOrderTestData.Speaking(Constants.Language.English));

    private static IServicePriceRepository PricedServices(params Currency[] currencies)
    {
        var mock = new Mock<IServicePriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(currencies
            .Select(c => ServicePrice.Create(CreateOrderTestData.ServiceId, c.Id, 500m, 100m))
            .AsQueryable()
            .BuildMock());
        return mock.Object;
    }

    private static IPackagePriceRepository PricedPackages(params Currency[] currencies)
    {
        var mock = new Mock<IPackagePriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(currencies
            .Select(c => PackagePrice.Create(CreateOrderTestData.PackageId, c.Id, 1000m))
            .AsQueryable()
            .BuildMock());
        return mock.Object;
    }

    private void VerifyCalculatorTold(string? currencyId, Times times) =>
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            currencyId,
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task AC1_HappyPath_Passes()
    {
        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The booking's language reaches the receipt and the audit row; a code the platform does not
    /// speak is refused here, the same way <c>Register</c> refuses it, so neither ever sees free text.
    /// </summary>
    [Theory]
    [InlineData("not-a-language my phone 777")]
    [InlineData("xx")]
    public async Task An_Unknown_Language_Is_Refused_With_LanguageNotSupported(string language)
    {
        var command = CreateOrderTestData.ValidCommand() with { Language = language };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(CreateOrder.Command.Language));
        Assert.Equal(BusinessErrorMessage.LanguageNotSupported, error.ErrorMessage);
        Assert.Equal(nameof(CreateOrder.Command.Language), error.ErrorCode);
    }

    [Fact]
    public async Task A_Null_Language_Is_Refused_As_Required_Not_Passed_Through()
    {
        var command = CreateOrderTestData.ValidCommand() with { Language = null! };

        var result = await CreateValidator().ValidateAsync(command);

        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(CreateOrder.Command.Language));
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
    }

    [Fact]
    public async Task AC2_BothAddressInputsSet_FailsAddressExactlyOneRequired()
    {
        var command = CreateOrderTestData.ValidCommand() with
        {
            CustomerAddress = CreateOrderTestData.InlineAddress(),
            SavedAddressId = "saved-1",
        };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.CustomerAddress)
            && e.ErrorMessage == BusinessErrorMessage.OrderAddressExactlyOneRequired);
    }

    [Fact]
    public async Task AC2_NeitherAddressInputSet_FailsAddressExactlyOneRequired()
    {
        var command = CreateOrderTestData.ValidCommand() with
        {
            CustomerAddress = null,
            SavedAddressId = null,
        };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.CustomerAddress)
            && e.ErrorMessage == BusinessErrorMessage.OrderAddressExactlyOneRequired);
    }

    [Fact]
    public async Task AC3_EmptyOrder_FailsEmptyOrder_BeforePriceCheck()
    {
        var command = CreateOrderTestData.ValidCommand(
            serviceIds: Array.Empty<string>(),
            packageIds: Array.Empty<string>());

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmptyOrder);
        // Cascade.Stop on the (empty-then-price) rule: the price check never runs for an empty order.
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
    }

    [Fact]
    public async Task AC4_PriceMismatch_FailsTotalPriceNotMatch()
    {
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing(totalPrice: 1500m));

        var command = CreateOrderTestData.ValidCommand(totalPrice: 1499m);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
    }

    [Fact]
    public async Task AC4_PriceMatch_PassesPriceCheck_WithCleaningDatePassedToCalculator()
    {
        var command = CreateOrderTestData.ValidCommand();

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            // The caller's currency — the same field the quote priced with.
            command.CurrencyId,
            command.CleaningDate,
            It.IsAny<string?>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------- the order's currency

    /// <summary>
    /// THE ORDER'S CURRENCY IS THE SERVICE ADDRESS'S COUNTRY'S (owner ruling 2026-09-12). With no
    /// currency named, a Slovak address is priced in EUR: the price re-check runs the calculator in
    /// EUR, and the offerable gate is asked about EUR. Nothing about the caller decides it.
    /// </summary>
    [Fact]
    public async Task No_Currency_Named_Resolves_The_Address_Countrys_Currency()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia)) with { CurrencyId = null };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        VerifyCalculatorTold(Eur.Id, Times.AtLeastOnce());
        _currencyRepository.Verify(r => r.IsOfferableAsync(Eur.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A named currency that IS the address country's passes — this is every shipped client, which
    /// echoes the currency the quote answered with.
    /// </summary>
    [Fact]
    public async Task A_Named_Currency_Equal_To_The_Address_Countrys_Reaches_The_Calculator()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia)) with { CurrencyId = Eur.Id };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        VerifyCalculatorTold(Eur.Id, Times.AtLeastOnce());
    }

    /// <summary>
    /// THE RULING, ENFORCED. A CZK currency on a Slovak address is refused with the currency key
    /// before anything is priced: the market is a property of the booking, and a client that moved the
    /// address after being quoted must re-quote. The failure is keyed to the field, and the calculator
    /// is never run.
    /// </summary>
    [Fact]
    public async Task A_Named_Currency_That_Is_Not_The_Address_Countrys_Is_Refused_Before_Pricing()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia))
            with { CurrencyId = CreateOrderTestData.CurrencyId };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        Assert.Equal(nameof(CreateOrder.Command.CurrencyId), failure.ErrorCode);
        VerifyCalculatorTold(It.IsAny<string?>(), Times.Never());
    }

    /// <summary>
    /// The offerable gate — switched on AND priced — runs on the RESOLVED currency: a country configured
    /// for a currency the platform does not yet operate is refused here rather than in the calculator,
    /// which throws on a currency it cannot price in and would turn the 400 into a 500. That is why
    /// the rule heads the price chain rather than standing alone — the class cascade is Continue.
    /// </summary>
    [Fact]
    public async Task A_Country_Currency_The_Platform_Cannot_Quote_In_Is_Refused_Before_Pricing()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Hungary)) with { CurrencyId = null };
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(Huf.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        Assert.Equal(nameof(CreateOrder.Command.CurrencyId), failure.ErrorCode);
        VerifyCalculatorTold(It.IsAny<string?>(), Times.Never());
    }

    /// <summary>
    /// The price re-check runs in the ADDRESS's currency, so a total carried over from a CZK quote is a
    /// price mismatch on a Slovak address, not a silently accepted CZK figure on a EUR order.
    /// </summary>
    [Fact]
    public async Task The_Price_Re_Check_Runs_In_The_Address_Countrys_Currency()
    {
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                Eur.Id,
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing(totalPrice: 60m) with
            {
                CurrencyId = Eur.Id, CurrencyCode = "EUR",
            });
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia),
            totalPrice: CreateOrderTestData.MatchingTotalPrice) with { CurrencyId = null };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    // ---------------------------------------------------------------- every entry priced in it

    /// <summary>
    /// A service picked from the CZK catalogue and then booked to a Slovak address has no EUR row. It
    /// is refused with the selection key — a 400 the wizard renders — and never reaches the
    /// calculator, which throws on exactly this and would surface as a 500.
    /// </summary>
    [Fact]
    public async Task A_Service_With_No_Price_Row_In_The_Order_Currency_Is_Refused()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia)) with { CurrencyId = null };

        var result = await CreateValidator(servicePrices: PricedServices(Czk)).ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
    }

    [Fact]
    public async Task A_Package_With_No_Price_Row_In_The_Order_Currency_Is_Refused()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia)) with { CurrencyId = null };

        var result = await CreateValidator(packagePrices: PricedPackages(Czk)).ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SelectedPackageIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedPackage);
    }

    /// <summary>
    /// ONE TRUTH PER REQUEST. A CZK-priced selection named in CZK on a Slovak address is a currency
    /// mismatch and nothing else: the item rules evaluate in the ADDRESS's currency, in which the
    /// selection has no rows, and without yielding they reported a second refusal about a market the
    /// caller never asked to book in. The client renders whichever error it reads first, so the
    /// second one is not merely noise -- it can hide the one the caller can act on.
    /// </summary>
    [Fact]
    public async Task A_Named_Currency_Of_Another_Market_Reports_Only_The_Currency_Error()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia))
            with { CurrencyId = CreateOrderTestData.CurrencyId };

        var result = await CreateValidator(servicePrices: PricedServices(Czk), packagePrices: PricedPackages(Czk))
            .ValidateAsync(command);

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvalidCurrency, failure.ErrorMessage);
        Assert.Equal(nameof(CreateOrder.Command.CurrencyId), failure.ErrorCode);
    }

    /// <summary>Anti-vacuity for the yield: named AND matching, the item rules still refuse an unpriced entry.</summary>
    [Fact]
    public async Task A_Named_Matching_Currency_Still_Refuses_An_Unpriced_Selection()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia)) with { CurrencyId = Eur.Id };

        var result = await CreateValidator(servicePrices: PricedServices(Czk), packagePrices: PricedPackages(Czk))
            .ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SelectedPackageIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedPackage);
    }

    // ---------------------------------------------------------------- a promo the server will not honour

    private const string PromoCode = "SAVE10";
    private const string PromoUser = "user-1";

    private void ArrangePromoPreview(decimal subtotal, string currencyId, PromoCodePreviewResult result)
    {
        _session.Setup(s => s.GetUserId()).Returns(PromoUser);
        _promoCodeService
            .Setup(s => s.PreviewAsync(PromoCode, PromoUser, subtotal, currencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private static PromoCodePreviewResult Refused(PromoCodeError error) => new(false, 0m, null, error);
    private static readonly PromoCodePreviewResult Honoured = new(true, 100m, "promo-1", null);

    private void VerifyPromoPreviewed(Times times) =>
        _promoCodeService.Verify(s => s.PreviewAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), times);

    /// <summary>
    /// THE REFUSAL. A code bound to CZK on a Slovak (EUR) booking is refused with the promo key,
    /// keyed to the field -- not silently booked at the full price the customer never consented to.
    /// The preview is asked in the ADDRESS country's currency, which is what the handler previews in.
    /// </summary>
    [Fact]
    public async Task A_Promo_The_Server_Will_Not_Honour_Refuses_The_Booking()
    {
        ArrangePromoPreview(CreateOrderTestData.MatchingTotalPrice, Eur.Id, Refused(PromoCodeError.CurrencyMismatch));
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia),
            promoCode: PromoCode) with { CurrencyId = null };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.PromoCurrencyMismatch, failure.ErrorMessage);
        Assert.Equal(nameof(CreateOrder.Command.PromoCode), failure.ErrorCode);
    }

    /// <summary>
    /// The preview runs against the same figures the handler's applier will use: the pre-surcharge
    /// subtotal from the ONE calculator run this validation made, in the resolved currency. A
    /// preview against the gross total would refuse a minimum the applier honours, and vice versa.
    /// </summary>
    [Fact]
    public async Task A_Promo_Is_Previewed_At_The_Pre_Surcharge_Subtotal_In_The_Address_Countrys_Currency()
    {
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing(totalPrice: 1800m) with
            {
                ExpressSurchargeApplied = true, ExpressSurchargeAmount = 300m,
            });
        ArrangePromoPreview(1500m, Eur.Id, Honoured);
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia),
            totalPrice: 1800m,
            promoCode: PromoCode) with { CurrencyId = null };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        _promoCodeService.Verify(s => s.PreviewAsync(
            PromoCode, PromoUser, 1500m, Eur.Id, It.IsAny<CancellationToken>()), Times.Once);
        VerifyCalculatorTold(Eur.Id, Times.Once());
    }

    [Fact]
    public async Task A_Promo_The_Server_Honours_Passes()
    {
        ArrangePromoPreview(CreateOrderTestData.MatchingTotalPrice, Czk.Id, Honoured);
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task No_Promo_Code_Consults_The_Promo_Service_For_Nothing()
    {
        _session.Setup(s => s.GetUserId()).Returns(PromoUser);

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand(promoCode: null));

        Assert.True(result.IsValid);
        VerifyPromoPreviewed(Times.Never());
    }

    /// <summary>
    /// The handler's applier keys every code to a customer and ignores one with no signed-in customer,
    /// which used to book the anonymous order at full price against the discounted total consented to.
    /// The code is refused instead, under its own key and before the promo service is asked anything.
    /// </summary>
    [Fact]
    public async Task A_Promo_With_No_Signed_In_Customer_Is_Refused_And_Never_Previewed()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand(promoCode: PromoCode));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.PromoRequiresAccount, failure.ErrorMessage);
        Assert.Equal(nameof(CreateOrder.Command.PromoCode), failure.ErrorCode);
        VerifyPromoPreviewed(Times.Never());
    }

    /// <summary>Anti-vacuity for the rule above: no code, no account, nothing to refuse.</summary>
    [Fact]
    public async Task An_Anonymous_Booking_Without_A_Promo_Passes()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand(promoCode: null));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// Cascade.Stop: the promo is previewed only once the price the customer consented to has been
    /// confirmed, so a stale total reports the price refusal alone and never reaches the promo service.
    /// </summary>
    [Fact]
    public async Task A_Price_Mismatch_Is_Reported_Alone_And_The_Promo_Is_Never_Previewed()
    {
        ArrangePromoPreview(CreateOrderTestData.MatchingTotalPrice, Czk.Id, Refused(PromoCodeError.Expired));
        var command = CreateOrderTestData.ValidCommand(totalPrice: 1499m, promoCode: PromoCode);

        var result = await CreateValidator().ValidateAsync(command);

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TotalPriceNotMatch, failure.ErrorMessage);
        VerifyPromoPreviewed(Times.Never());
    }

    [Theory]
    [InlineData(PromoCodeError.NotFound, BusinessErrorMessage.PromoNotFound)]
    [InlineData(PromoCodeError.Inactive, BusinessErrorMessage.PromoInactive)]
    [InlineData(PromoCodeError.Expired, BusinessErrorMessage.PromoExpired)]
    [InlineData(PromoCodeError.NotYetValid, BusinessErrorMessage.PromoNotYetValid)]
    [InlineData(PromoCodeError.GlobalLimitReached, BusinessErrorMessage.PromoGlobalLimitReached)]
    [InlineData(PromoCodeError.PerUserLimitReached, BusinessErrorMessage.PromoPerUserLimitReached)]
    [InlineData(PromoCodeError.BelowMinimumOrderAmount, BusinessErrorMessage.PromoBelowMinimumOrderAmount)]
    [InlineData(PromoCodeError.CurrencyMismatch, BusinessErrorMessage.PromoCurrencyMismatch)]
    public async Task Every_Preview_Refusal_Is_Reported_Under_Its_Own_Promo_Key(PromoCodeError error, string expectedKey)
    {
        ArrangePromoPreview(CreateOrderTestData.MatchingTotalPrice, Czk.Id, Refused(error));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        var result = await CreateValidator().ValidateAsync(command);

        var failure = Assert.Single(result.Errors);
        Assert.Equal(expectedKey, failure.ErrorMessage);
        Assert.Equal(nameof(CreateOrder.Command.PromoCode), failure.ErrorCode);
    }

    /// <summary>
    /// The mapping must cover the whole enum: a value added to PromoCodeError without an arm would
    /// throw out of the validator, and a rule whose template placeholder was never filled would ship
    /// the placeholder as the message. Distinct keys, because two refusals sharing one string is a
    /// customer told the wrong reason.
    /// </summary>
    [Fact]
    public async Task Every_PromoCodeError_Value_Maps_To_A_Distinct_Promo_Key()
    {
        var keys = new List<string>();
        foreach (var error in Enum.GetValues<PromoCodeError>())
        {
            ArrangePromoPreview(CreateOrderTestData.MatchingTotalPrice, Czk.Id, Refused(error));

            var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand(promoCode: PromoCode));

            var failure = Assert.Single(result.Errors);
            Assert.StartsWith("promo.", failure.ErrorMessage);
            keys.Add(failure.ErrorMessage);
        }

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public async Task AC5_PastCleaningDate_FailsCleaningDateInFuture()
    {
        var command = CreateOrderTestData.ValidCommand(cleaningDate: DateTime.UtcNow.AddHours(-1));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateInFuture);
    }

    [Fact]
    public async Task AC5_FutureDateBelowLeadTime_FailsCleaningDateBelowLeadTime()
    {
        // ExpressLeadTimeHours is 2h — one hour out is in the future but below the minimum lead time.
        var command = CreateOrderTestData.ValidCommand(cleaningDate: DateTime.UtcNow.AddHours(1));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateBelowLeadTime);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeIneligible_WithLoggedInUser_FailsPreferredEmployeeNotEligible()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        ArrangeActiveMembership("user-1");
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                "user-1", "emp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.PreferredEmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeNotEligible);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeSet_NoUserId_FailsMembershipRequired()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.PreferredEmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeMembershipRequired);
        _orderRepository.Verify(r => r.UserHasCompletedOrderWithEmployeeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeSet_NoMembership_FailsMembershipRequired()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        _userMembershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.PreferredEmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeMembershipRequired);
        _orderRepository.Verify(r => r.UserHasCompletedOrderWithEmployeeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeEligible_WithLoggedInUser_Passes()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        ArrangeActiveMembership("user-1");
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                "user-1", "emp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    // ── SpecialInstructions — optional free-text, capped at 2000 ──

    [Fact]
    public async Task SpecialInstructions_Omitted_Passes()
    {
        var result = await CreateValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(specialInstructions: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SpecialInstructions_AtMaxLength_Passes()
    {
        var command = CreateOrderTestData.ValidCommand(
            specialInstructions: new string('x', 2000));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SpecialInstructions_OverMaxLength_FailsMaxLength()
    {
        var command = CreateOrderTestData.ValidCommand(
            specialInstructions: new string('x', 2001));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SpecialInstructions)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    // ── AccessInstructions — optional free-text, capped at 2000 ──

    [Fact]
    public async Task AccessInstructions_Omitted_Passes()
    {
        var result = await CreateValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(accessInstructions: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AccessInstructions_AtMaxLength_Passes()
    {
        var command = CreateOrderTestData.ValidCommand(
            accessInstructions: new string('x', 2000));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AccessInstructions_OverMaxLength_FailsMaxLength()
    {
        var command = CreateOrderTestData.ValidCommand(
            accessInstructions: new string('x', 2001));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.AccessInstructions)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    private void ArrangeActiveMembership(string userId)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            discountPercentage: 10m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);

        _userMembershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: userId,
                membershipPlanId: plan.Id,
                currencyId: "currency-czk",
                stripeSubscriptionId: "sub_1",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));
    }
}
