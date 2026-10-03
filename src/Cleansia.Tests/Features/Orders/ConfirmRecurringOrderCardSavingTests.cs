using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-01: confirming a recurring occurrence by card on the web keeps the card only when the
/// customer ticked "save this card for my next bookings". Ticked, the Checkout Session is opened on the
/// customer's Stripe Customer for the order's currency with the saved card recorded under the consent, as a web
/// booking records one; unticked, nothing is kept. A guest is refused the tick. The mobile channel takes the
/// tick on its PaymentSheet intent, so its confirmation keeps nothing.
/// </summary>
public sealed class ConfirmRecurringOrderCardSavingTests
{
    private const string OrderId = "order-recurring-card-saving";
    private const string CustomerUserId = "user-recurring-card-saving";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IStripeCustomerResolver> _stripeCustomers = new();
    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly List<SavedCard> _addedCards = [];
    private readonly Currency _czk = Currency.Create("CZK", "Kč", "Czech koruna");
    private readonly User _user;
    private readonly Order _order;

    public ConfirmRecurringOrderCardSavingTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        _user = User.CreateWithPassword("jana.novakova@example.com", "Passw0rd!", "Jana", "Nováková");
        _user.Id = CustomerUserId;
        _user.TenantId = "company-of-the-account";
        _user.AssignStripeCustomerId("cus_legacy");
        _users.Setup(r => r.GetByIdAsync(CustomerUserId, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _stripeCustomers
            .Setup(r => r.ResolveForCurrencyAsync(_user, _czk, It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_czk");
        _savedCards.Setup(r => r.Add(It.IsAny<SavedCard>())).Callback<SavedCard>(_addedCards.Add);
        _requestMetadata.SetupGet(m => m.IpAddress).Returns("203.0.113.9");
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns("Firefox on Windows");
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_plain", "https://checkout.stripe.test/cs_plain"));
        _stripe
            .Setup(s => s.CreateCardSavingCheckoutSessionAsync(
                It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_saving", "https://checkout.stripe.test/cs_saving"));
        _stripe
            .Setup(s => s.CreatePaymentIntentAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), OrderId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("pi_1", "pi_1_secret"));
        _stripe.Setup(s => s.CreateEphemeralKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("ek_1");

        _order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", "country-cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 990m,
            currencyId: _czk.Id,
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerUserId,
            recurringTemplateId: "tmpl-weekly",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        _order.Id = OrderId;
        _order.TenantId = "company-of-the-order";
        _order.SetCurrency(_czk);
        _order.UpdateEstimatedTime(120);
        _order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
        _order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, _order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(_order);
    }

    private ConfirmRecurringOrder.Handler Handler(OrderChannel channel) => new(
        OrderAccessDoubles.Over(_orderRepository, _session),
        _orderRepository.Object,
        _savedCards.Object,
        Mock.Of<IReceivableRepository>(),
        Mock.Of<ICreditAccountRepository>(),
        _users.Object,
        _session.Object,
        Mock.Of<ITenantProvider>(),
        _stripe.Object,
        new StripeConfig(new ConfigurationBuilder().Build()),
        _stripeCustomers.Object,
        _requestMetadata.Object,
        new OrderChannelProvider(channel),
        Mock.Of<IPendingDispatch>(),
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        Mock.Of<IConsentService>(),
        Legal.CustomerConsentDoubles.Consented(),
        Mock.Of<ILegalDocumentResolver>(),
        new AuditContext(),
        NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    [Fact]
    public async Task Ticked_On_The_Web_The_Card_Is_Recorded_Under_The_Consent_And_The_Session_Asks_To_Keep_It()
    {
        var result = await Handler(OrderChannel.Web).Handle(
            new ConfirmRecurringOrder.Command(OrderId, SaveCard: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var card = Assert.Single(_addedCards);
        Assert.Equal(
            (CustomerUserId, _czk.Id, "cus_czk", SavedCard.ConsentTextVersionInForce, "203.0.113.9", "Firefox on Windows"),
            (card.UserId, card.CurrencyId, card.StripeCustomerId, card.ConsentTextVersion, card.ConsentIpAddress, card.ConsentDeviceLabel));
        Assert.Equal("company-of-the-account", card.TenantId);
        Assert.False(card.IsCaptured);
        _stripe.Verify(s => s.CreateCardSavingCheckoutSessionAsync(
            _order, It.IsAny<DateTime>(), "cus_czk", card.Id, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(s => s.CreateCheckoutSessionAsync(
            It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("https://checkout.stripe.test/cs_saving", result.Value!.CheckoutUrl);
        Assert.Equal("cs_saving", _order.StripeSessionId);
    }

    [Fact]
    public async Task Unticked_On_The_Web_Nothing_Is_Kept()
    {
        var result = await Handler(OrderChannel.Web).Handle(
            new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_addedCards);
        _stripe.Verify(s => s.CreateCheckoutSessionAsync(
            _order, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(s => s.CreateCardSavingCheckoutSessionAsync(
            It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _stripeCustomers.Verify(
            r => r.ResolveForCurrencyAsync(It.IsAny<User>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Confirming again with the tick still on gets the order's open session back, which already names the
    /// saved card it was opened with, so no second card is recorded.
    /// </summary>
    [Fact]
    public async Task Ticked_Again_While_The_Session_Is_Handed_Back_Records_No_Second_Card()
    {
        _order.AssignStripeSessionId("cs_saving");

        var result = await Handler(OrderChannel.Web).Handle(
            new ConfirmRecurringOrder.Command(OrderId, SaveCard: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_addedCards);
        Assert.Equal("cs_saving", _order.StripeSessionId);
    }

    [Fact]
    public async Task Ticked_On_Mobile_The_Confirmation_Keeps_Nothing_Since_The_Sheet_Intent_Takes_The_Tick()
    {
        var result = await Handler(OrderChannel.Mobile).Handle(
            new ConfirmRecurringOrder.Command(OrderId, SaveCard: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_addedCards);
        _stripe.Verify(s => s.CreatePaymentIntentAsync(
            It.IsAny<decimal>(), "CZK", "cus_legacy", OrderId, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Guest_Asking_To_Save_The_Card_Is_Refused()
    {
        var guest = new Mock<IUserSessionProvider>();
        guest.Setup(s => s.GetUserId()).Returns((string?)null);

        var result = await new ConfirmRecurringOrder.Validator(
                guest.Object, OrderAccessDoubles.Over(_orderRepository, guest),
                Legal.CustomerConsentDoubles.Consented(), Mock.Of<ILegalDocumentResolver>())
            .ValidateAsync(new ConfirmRecurringOrder.Command(OrderId, SaveCard: true));

        Assert.Equal(BusinessErrorMessage.SavedCardRequiresAccount, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Signed_In_Customer_May_Ask_To_Save_The_Card()
    {
        var result = await new ConfirmRecurringOrder.Validator(
                _session.Object, OrderAccessDoubles.Over(_orderRepository, _session),
                Legal.CustomerConsentDoubles.Consented(), Mock.Of<ILegalDocumentResolver>())
            .ValidateAsync(new ConfirmRecurringOrder.Command(OrderId, SaveCard: true));

        Assert.True(result.IsValid);
    }
}
