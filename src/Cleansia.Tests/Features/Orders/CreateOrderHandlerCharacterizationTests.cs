using Microsoft.Extensions.Configuration;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Cleansia.Tests.Common;
using Cleansia.Tests.Domain.Legal;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Characterization of <c>CreateOrder.Handler</c> as it stands after the Wave-0 F2 change (post-commit
/// dispatch via <see cref="IPendingDispatch"/>) and before the AUD-06 decomposition. Pins the
/// observable handler behavior — saved-address ownership masking, the serviced-city/country gates, the
/// Cash receipt enqueue at the outbox seam, and the Card/Stripe error mapping — so the future split can
/// be proven behavior-preserving.
/// </summary>
public class CreateOrderHandlerCharacterizationTests
{
    private const string UserId = "user-1";
    private const string CreatedOrderId = "order-created-1";
    private const string ConfirmationCode = "ABC123";

    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IServiceCityRepository> _serviceCityRepository = new();
    private readonly Mock<IStripeClientFactory> _stripeClientFactory = new();
    private readonly Mock<IStripeClient> _stripeClient = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<IPromoCodeService> _promoCodeService = new();
    private readonly Mock<IReferralService> _referralService = new();
    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IAddressGeocoder> _addressGeocoder = new();

    public CreateOrderHandlerCharacterizationTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        _stripeClientFactory.Setup(f => f.CreateClient()).Returns(_stripeClient.Object);
        // A default session for the tests that are not about Stripe at all. Moq's loose default
        // was a null result, which was survivable while the dispatcher discarded it and is not now
        // that it reads the session's id to record the order's charge surface. The tests that DO
        // care about the session override this.
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(
                It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult(
                "cs_test_default", "https://checkout.stripe.com/c/pay/cs_test_default"));

        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderInput input, CancellationToken _) =>
                OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
                {
                    Id = CreatedOrderId,
                    UserId = input.UserId,
                    PaymentType = input.PaymentType,
                    TotalPrice = input.RawSubtotal,
                    CustomerAddress = input.Address,
                    TenantId = "tenant-1",
                }));
    }

    /// <summary>The fixture's "cz" trades in CZK, Slovakia in EUR; anything else is the platform default.</summary>
    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency Eur = Market(Currency.Create("EUR", "€", "Euro"), "currency-eur");
    private const string Slovakia = "sk";

    private static Currency Market(Currency currency, string id)
    {
        currency.Id = id;
        currency.IsActive = true;
        return currency;
    }

    private CreateOrder.Handler CreateHandler(OrderChannel channel = OrderChannel.Web) =>
        new(
            OrderMarketDoubles.Trading(Czk, ("cz", Czk), (Slovakia, Eur)),
            _session.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            new OrderAddressResolver(
                _addressRepository.Object,
                _savedAddressRepository.Object,
                _countryRepository.Object,
                _serviceCityRepository.Object,
                _addressGeocoder.Object),
            new OrderPromoApplier(
                _promoCodeService.Object,
                NullLogger<OrderPromoApplier>.Instance),
            new OrderLateReferralAcceptor(
                _referralService.Object,
                _referralRepository.Object,
                NullLogger<OrderLateReferralAcceptor>.Instance),
            new OrderPaymentDispatcher(
                _stripeClientFactory.Object,
                _pending.Object,
                new OrderChannelProvider(channel),
                new StripeConfig(new ConfigurationBuilder().Build()),
                NullLogger<OrderPaymentDispatcher>.Instance),
            _expressWaiverConsumer.Object,
            // No credit account: these suites characterize pricing, dispatch and the waiver slot, and
            // an unconfigured Mock returns null from GetSpendableAsync - which is exactly what a
            // customer who has never been credited looks like, and what every case here assumes.
            _creditAccountRepository.Object,
            new CancellationPolicyResolver(new Mock<IUserMembershipRepository>().Object),
            LegalDocumentFixtures.Resolver().Object,
            new AuditContext(),
            NullLogger<CreateOrder.Handler>.Instance);

    private void ArrangeSavedAddress(string savedAddressId, string ownerUserId, Address? resolved = null)
    {
        var saved = SavedAddressMockFactory.Generate(new SavedAddressMockFactory.SavedAddressPartial
        {
            Id = savedAddressId,
            UserId = ownerUserId,
            AddressId = "address-1",
        });
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(savedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        _addressRepository
            .Setup(r => r.GetByIdAsync("address-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved ?? AddressMockFactory.Generate());
    }

    [Fact]
    public async Task AC7_SavedAddress_OwnedByDifferentUser_ReturnsNotFound()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: "another-user");

        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Error!.Message);
    }

    [Fact]
    public async Task AC7_SavedAddress_NotFound_ReturnsNotFound()
    {
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavedAddress?)null);

        var command = CreateOrderTestData.ValidCommand(savedAddressId: "missing");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Error!.Message);
    }

    [Fact]
    public async Task AC8_CityNotServiced_ReturnsCityNotServiced()
    {
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateOrderTestData.ValidCommand();

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CityNotServiced, result.Error!.Message);
        Assert.Equal(nameof(Address.City), result.Error.Code);
    }

    [Fact]
    public async Task AC8_SavedAddress_CountryNoLongerServiced_ReturnsCountryNotServiced()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: UserId);
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, result.Error!.Message);
    }

    [Fact]
    public async Task AC9_CashPath_EnqueuesGenerateReceipt_AndStripeSessionIdIsNull()
    {
        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Cash);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.StripeSessionId);
        _pending.Verify(p => p.Enqueue(
            QueueNames.GenerateReceipt,
            It.Is<QueueEnvelope<GenerateReceiptMessage>>(e =>
                e.Payload.OrderId == CreatedOrderId
                && e.Payload.LanguageCode == command.Language),
            MessageKeys.Receipt(CreatedOrderId)),
            Times.Once);
    }

    [Fact]
    public async Task AC9_CashPath_DoesNotCreateStripeSession()
    {
        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Cash);

        await CreateHandler().Handle(command, CancellationToken.None);

        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AC10_CardPath_PopulatesStripeSessionId_AndDoesNotEnqueueReceipt()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_test_session", "https://checkout.stripe.com/c/pay/cs_test_session"));

        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "https://checkout.stripe.com/c/pay/cs_test_session", result.Value!.StripeSessionId);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
            It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task MobileChannel_CardPath_StripeSessionIdIsNull_AndDoesNotCreateCheckoutSession()
    {
        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card);

        var result = await CreateHandler(OrderChannel.Mobile).Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.StripeSessionId);
        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WebChannel_CardPath_StillCreatesCheckoutSession()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_test_session", "https://checkout.stripe.com/c/pay/cs_test_session"));

        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card);

        var result = await CreateHandler(OrderChannel.Web).Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "https://checkout.stripe.com/c/pay/cs_test_session", result.Value!.StripeSessionId);
        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AC10_CardPath_StripeException_ReturnsPaymentGatewayUnavailable()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));

        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    /// <summary>
    /// The customer's booking note reached the API and stopped there — the handler
    /// built its <see cref="CreateOrderInput"/> without it. Pin the forwarding.
    /// </summary>
    [Fact]
    public async Task SpecialInstructions_AreForwardedToTheOrderFactory()
    {
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        var command = CreateOrderTestData.ValidCommand(
            specialInstructions: "Gate code 1234, please ring twice.");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Gate code 1234, please ring twice.", captured!.SpecialInstructions);
    }

    [Fact]
    public async Task SpecialInstructions_OmittedByOlderClients_ArriveAsNull()
    {
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        await CreateHandler().Handle(CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.Null(captured!.SpecialInstructions);
    }

    /// <summary>
    /// Same defect one field over: <c>AccessInstructions</c> was a column every
    /// partner surface rendered and no code path ever wrote. Pin the forwarding.
    /// </summary>
    [Fact]
    public async Task AccessInstructions_AreForwardedToTheOrderFactory()
    {
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        var command = CreateOrderTestData.ValidCommand(
            accessInstructions: "Key is in the lockbox, code 4455.");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Key is in the lockbox, code 4455.", captured!.AccessInstructions);
    }

    [Fact]
    public async Task AccessInstructions_OmittedByOlderClients_ArriveAsNull()
    {
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        await CreateHandler().Handle(CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.Null(captured!.AccessInstructions);
    }

    // ---------------------------------------------------------------- the order's currency

    /// <summary>
    /// THE ORDER IS STAMPED WITH THE SERVICE ADDRESS'S COUNTRY'S CURRENCY (owner ruling 2026-09-12),
    /// and priced with that row's id, so the price and the stamp cannot name different currencies. A
    /// Slovak address is a EUR order whatever the command says: the validator has already refused a
    /// named currency that disagrees, so the handler reads the country and nothing else.
    /// </summary>
    [Fact]
    public async Task The_Order_Is_Stamped_With_The_Address_Countrys_Currency()
    {
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia)) with { CurrencyId = null };
        await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Same(Eur, captured!.Currency);
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
            Eur.Id, It.IsAny<DateTime?>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// The saved-address path reads the same country: a saved Slovak address books a EUR order.
    /// </summary>
    [Fact]
    public async Task A_Saved_Address_Is_Stamped_With_Its_Countrys_Currency()
    {
        ArrangeSavedAddress("saved-sk", ownerUserId: UserId,
            resolved: AddressMockFactory.Generate(new AddressMockFactory.AddressPartial
            {
                CountryId = Slovakia, Latitude = 48.14, Longitude = 17.10,
            }));
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-sk") with { CurrencyId = null };
        await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Same(Eur, captured!.Currency);
    }

    [Fact]
    public async Task A_Czech_Address_Is_Stamped_With_The_Platform_Default()
    {
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        var command = CreateOrderTestData.ValidCommand() with { CurrencyId = null };
        await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Same(Czk, captured!.Currency);
    }

    /// <summary>
    /// The validator refuses a promo the preview will not honour, and the handler re-runs the SAME
    /// preview -- the same code, the pre-surcharge subtotal, the address country's currency -- so the
    /// two cannot disagree. The service is set up on exactly those arguments: a handler that previewed
    /// on a different subtotal or currency would get no answer here and book without the discount.
    /// </summary>
    [Fact]
    public async Task A_Promo_The_Validator_Honoured_Is_Applied_From_The_Same_Preview()
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
        _promoCodeService
            .Setup(s => s.PreviewAsync("SAVE10", UserId, 1500m, Eur.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodePreviewResult(true, 100m, "promo-1", null));
        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                TenantId = "tenant-1",
            }));

        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: Slovakia),
            totalPrice: 1800m,
            promoCode: "SAVE10") with { CurrencyId = null };
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, captured!.PromoDiscountAmount);
        Assert.Equal("promo-1", captured.PromoCodeId);
    }

    [Fact]
    public async Task AC10_CardPath_NonStripeException_IsNotCaught_Bubbles()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bad order state"));

        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
