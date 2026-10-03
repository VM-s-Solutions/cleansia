using System.Globalization;
using System.Net;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
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
/// Owner rulings 2026-09-28. The customer's confirmation of a recurring occurrence is its own marker,
/// stamped for both tenders: a cash occurrence is confirmed by it and stays unpaid until the cleaner
/// records the cash; a card occurrence is confirmed only once its payment settles, so it may be confirmed
/// again after an abandoned payment. The web, which has no PaymentSheet, pays through a Checkout Session
/// that closes before the occurrence is retracted; one order never gets a charge surface on each channel.
/// </summary>
public sealed class ConfirmRecurringOrderMarkerAndChannelTests
{
    private const string OrderId = "order-recurring-marker";
    private const string CustomerUserId = "user-recurring-marker";
    private const string WebhookSecret = "whsec_recurring_marker";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<ICreditAccountRepository> _credits = new();
    private readonly List<(DateTime ExpiresAt, decimal AmountDue)> _checkouts = [];

    public ConfirmRecurringOrderMarkerAndChannelTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        var user = User.CreateWithPassword("jana.novakova@example.com", "Passw0rd!", "Jana", "Nováková");
        user.Id = CustomerUserId;
        user.AssignStripeCustomerId("cus_existing");
        _users.Setup(r => r.GetByIdAsync(CustomerUserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _stripe
            .Setup(s => s.CreatePaymentIntentAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), "cus_existing", OrderId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("pi_1", "pi_1_secret"));
        _stripe
            .Setup(s => s.CreateEphemeralKeyAsync("cus_existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ek_1");
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Order, DateTime, CancellationToken>((order, expiresAt, _) => _checkouts.Add((expiresAt, order.AmountDueOnCard)))
            .ReturnsAsync(new CheckoutSessionResult("cs_1", "https://checkout.stripe.test/cs_1"));
    }

    private ConfirmRecurringOrder.Handler Handler(OrderChannel channel) => new(
        OrderAccessDoubles.Over(_orderRepository, _session),
        _orderRepository.Object,
        SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>(),
        _credits.Object,
        _users.Object,
        _session.Object,
        Mock.Of<ITenantProvider>(),
        _stripe.Object,
        new StripeConfig(new ConfigurationBuilder().Build()),
        Mock.Of<IStripeCustomerResolver>(),
        Mock.Of<IRequestMetadataProvider>(),
        new OrderChannelProvider(channel),
        _pending.Object,
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        new AuditContext(),
        NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private Order ArrangeOccurrence(PaymentType paymentType, DateTime? cleaningDateTime = null)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", "country-cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 990m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerUserId,
            recurringTemplateId: "tmpl-weekly",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.TenantId = "company-of-the-order";
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.UpdateEstimatedTime(120);
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    [Fact]
    public async Task A_Cash_Confirmation_Stamps_The_Marker_And_Makes_The_Occurrence_Offerable_Unpaid()
    {
        var order = ArrangeOccurrence(PaymentType.Cash);
        Assert.False(OrderAvailability.IsOfferable(order));

        var result = await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.CheckoutUrl);
        Assert.NotNull(order.CustomerConfirmedAt);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.True(OrderAvailability.IsOfferable(order));
        Assert.False(order.AwaitsCustomerConfirmation);
    }

    [Fact]
    public async Task A_Mobile_Card_Confirmation_Stamps_The_Marker_And_Stays_Confirmable_Until_Paid()
    {
        var order = ArrangeOccurrence(PaymentType.Card);

        var result = await Handler(OrderChannel.Mobile).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("pi_1_secret", result.Value!.ClientSecret);
        Assert.NotNull(order.CustomerConfirmedAt);
        Assert.True(order.AwaitsCustomerConfirmation);
        Assert.False(OrderAvailability.IsOfferable(order));

        var retry = await Handler(OrderChannel.Mobile).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
    }

    [Fact]
    public async Task A_Web_Card_Confirmation_Returns_A_Checkout_Session_And_Mints_No_PaymentIntent()
    {
        var order = ArrangeOccurrence(PaymentType.Card);

        var result = await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("https://checkout.stripe.test/cs_1", result.Value!.CheckoutUrl);
        Assert.Null(result.Value.ClientSecret);
        Assert.Equal("cs_1", order.StripeSessionId);
        Assert.Null(order.StripePaymentIntentId);
        Assert.NotNull(order.CustomerConfirmedAt);
        _stripe.Verify(s => s.CreatePaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The web checkout is keyed on its expiry. Resume asks with Stripe's default lifetime and key, so it
    /// would open a second session beside it, open past the cutoff, rather than replay it.
    /// </summary>
    [Fact]
    public async Task A_Web_Confirmed_Occurrence_Is_Not_Resumed_Beside_Its_Checkout()
    {
        var order = ArrangeOccurrence(PaymentType.Card);
        await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        Assert.Equal("cs_1", order.StripeSessionId);
        _orderRepository
            .Setup(r => r.GetByIdForOwnerAsync(OrderId, CustomerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var resume = await new ResumeOrderCheckout.Validator(_orderRepository.Object, _session.Object)
            .ValidateAsync(new ResumeOrderCheckout.Command(OrderId));

        Assert.Equal(BusinessErrorMessage.InvalidOrderStatusTransition, Assert.Single(resume.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(OrderChannel.Web)]
    [InlineData(OrderChannel.Mobile)]
    public async Task A_Card_Occurrence_Begun_On_The_Other_Channel_Is_Refused(OrderChannel channel)
    {
        var order = ArrangeOccurrence(PaymentType.Card);
        if (channel == OrderChannel.Web)
        {
            order.AssignStripePaymentIntentId("pi_mobile");
        }
        else
        {
            order.AssignStripeSessionId("cs_web");
        }

        var result = await Handler(channel).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidOrderStatusTransition, result.Error!.Message);
        Assert.Null(order.CustomerConfirmedAt);
        _stripe.Verify(s => s.CreateCheckoutSessionAsync(
            It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _stripe.Verify(s => s.CreatePaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The auto-cancel sweep leaves an occurrence Pending, so the payment guard alone let a customer
    /// confirm — and a card customer pay for — an occurrence already cancelled.
    /// </summary>
    [Theory]
    [InlineData(PaymentType.Cash)]
    [InlineData(PaymentType.Card)]
    public async Task A_Cancelled_Occurrence_Is_Refused(PaymentType paymentType)
    {
        var order = ArrangeOccurrence(paymentType);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

        var result = await Handler(OrderChannel.Mobile).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCancelled, result.Error!.Message);
        Assert.Null(order.CustomerConfirmedAt);
        _pending.VerifyNoOtherCalls();
    }

    /// <summary>
    /// AutoCancelStaleRecurringOrders retracts an unpaid occurrence an hour before its slot, and a payment
    /// after that is for a clean nobody will do, so the session may not outlive that cutoff. Every confirm
    /// inside one 23-hour stride back from the cutoff names the same expiry, so Stripe replays one session.
    /// </summary>
    [Fact]
    public async Task A_Web_Checkout_Closes_By_The_Occurrences_Cutoff_And_A_Second_Confirm_Asks_For_The_Same_Session()
    {
        var cleaning = DateTime.UtcNow.AddDays(3).AddHours(6);
        ArrangeOccurrence(PaymentType.Card, cleaning);
        var cutoff = cleaning.AddHours(-AutoCancelStaleRecurringOrders.DefaultMissedConfirmGraceHours);

        await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.Equal(2, _checkouts.Count);
        Assert.Equal(cutoff.AddHours(-3 * 23), _checkouts[0].ExpiresAt);
        Assert.Equal(_checkouts[0].ExpiresAt, _checkouts[1].ExpiresAt);
        Assert.InRange(_checkouts[0].ExpiresAt, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow.AddHours(24));
    }

    /// <summary>
    /// In a stride's last half hour Stripe will not open a session that short. Unless one is already open
    /// for the stride, and so replayed, the next stride's session opens instead.
    /// </summary>
    [Fact]
    public async Task In_The_Last_Half_Hour_Of_A_Stride_The_Next_Stride_Opens_The_Session()
    {
        var cleaning = DateTime.UtcNow.AddHours(2 * 23 + 1).AddMinutes(10);
        var order = ArrangeOccurrence(PaymentType.Card, cleaning);
        var tooShort = cleaning.AddHours(-1 - 2 * 23);
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), tooShort, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Stripe.StripeException(
                HttpStatusCode.BadRequest,
                new Stripe.StripeError { Type = "invalid_request_error", Param = "expires_at" },
                "The `expires_at` timestamp must be at least 30 minutes from Checkout Session creation."));
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), tooShort.AddHours(23), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_next", "https://checkout.stripe.test/cs_next"));

        var result = await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("https://checkout.stripe.test/cs_next", result.Value!.CheckoutUrl);
        Assert.Equal("cs_next", order.StripeSessionId);
    }

    [Fact]
    public async Task A_Stripe_Failure_On_The_Web_Is_Gateway_Unavailable_And_Records_No_Session()
    {
        var order = ArrangeOccurrence(PaymentType.Card);
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Stripe.StripeException("Stripe is unreachable"));

        var result = await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
        Assert.True(string.IsNullOrEmpty(order.StripeSessionId));
        _stripe.Verify(s => s.CreateCheckoutSessionAsync(
            It.IsAny<Order>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Credit_Taken_On_The_Web_Leaves_The_Checkout_Asking_Only_For_The_Rest()
    {
        var order = ArrangeOccurrence(PaymentType.Card);
        _credits
            .Setup(c => c.GetSpendableAsync(CustomerUserId, order.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditSpendable("credit-1", 200m, order.CurrencyId, null));
        _credits
            .Setup(c => c.TryDebitAsync(
                "credit-1", 200m, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", CustomerUserId,
                It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()))
            .ReturnsAsync(true);

        var result = await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(200m, order.CreditAppliedAmount);
        Assert.Equal(990m - 200m, Assert.Single(_checkouts).AmountDue);
    }

    /// <summary>
    /// An abandoned web checkout expires unpaid. It does not cancel the occurrence, as it cancels an
    /// abandoned one-off booking: the occurrence waits for the recurring cutoff like one whose PaymentSheet
    /// was closed, and the app may now pay it.
    /// </summary>
    [Fact]
    public async Task A_Web_Checkout_That_Expired_Leaves_The_Occurrence_Confirmable_On_Either_Channel()
    {
        var order = ArrangeOccurrence(PaymentType.Card);
        await Handler(OrderChannel.Web).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        Assert.Equal("cs_1", order.StripeSessionId);

        var expired = await WebhookHandler(order).Handle(ExpiredSession("cs_1"), CancellationToken.None);

        Assert.True(expired.IsSuccess);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.CancelledAt);
        Assert.True(order.AwaitsCustomerConfirmation);

        var mobile = await Handler(OrderChannel.Mobile).Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(mobile.IsSuccess, mobile.Error?.Message);
        Assert.Equal("pi_1_secret", mobile.Value!.ClientSecret);
    }

    private HandlePaymentNotification.Handler WebhookHandler(Order order)
    {
        _orderRepository.Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var config = new Mock<IStripeConfig>();
        config.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        var processed = new Mock<IProcessedStripeEventRepository>();
        processed.Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        return new HandlePaymentNotification.Handler(
            config.Object,
            _orderRepository.Object,
            Mock.Of<ICreditAccountRepository>(),
            Mock.Of<IDisputeRepository>(),
            processed.Object,
            Mock.Of<IStripeSubscriptionWebhookHandler>(),
            Mock.Of<ITenantProvider>(),
            _pending.Object,
            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
            Mock.Of<INotificationProducer>(),
            NoPreferredCleanerHold.Resolver,
            Mock.Of<IAdminNotifier>(),
            Mock.Of<IUserNotificationRepository>(),
            Mock.Of<IStripeClientFactory>(),
            Mock.Of<ITenantRepository>(),
            Mock.Of<ISavedCardRepository>(),
            Mock.Of<IReceivableRepository>(),
            NullLogger<HandlePaymentNotification.Handler>.Instance);
    }

    private static HandlePaymentNotification.Command ExpiredSession(string sessionId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $$"""
        {
          "id": "evt_recurring_expired",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Constants.StripeEventType.ExpiredSession}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "payment_status": "unpaid",
              "metadata": { "OrderId": "{{OrderId}}" }
            },
            "previous_attributes": null
          }
        }
        """;
        var timestamp = created.ToString(CultureInfo.InvariantCulture);
        var signature = Stripe.EventUtility.ComputeSignature(WebhookSecret, timestamp, payload);
        return new HandlePaymentNotification.Command(payload, $"t={timestamp},v1={signature}");
    }
}
