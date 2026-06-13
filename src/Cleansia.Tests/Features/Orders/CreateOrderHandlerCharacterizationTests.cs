using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
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
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IServiceCityRepository> _serviceCityRepository = new();
    private readonly Mock<IStripeClientFactory> _stripeClientFactory = new();
    private readonly Mock<IStripeClient> _stripeClient = new();
    private readonly Mock<IPendingDispatch> _pending = new();
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

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        _stripeClientFactory.Setup(f => f.CreateClient()).Returns(_stripeClient.Object);

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

    private CreateOrder.Handler CreateHandler() =>
        new(
            _addressRepository.Object,
            _savedAddressRepository.Object,
            _currencyRepository.Object,
            _countryRepository.Object,
            _serviceCityRepository.Object,
            _stripeClientFactory.Object,
            _pending.Object,
            _promoCodeService.Object,
            _referralService.Object,
            _referralRepository.Object,
            _session.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            _addressGeocoder.Object,
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
            .ReturnsAsync("cs_test_session");

        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("cs_test_session", result.Value!.StripeSessionId);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
            It.IsAny<string>()),
            Times.Never);
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
