using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// A saved card lands through the webhook on the row its capture started: from the mobile PaymentSheet
/// as setup_intent.succeeded, from the web as a setup-mode checkout.session.completed, and from a card
/// payment the customer ticked "save this card" on as payment_intent.succeeded or a payment-mode
/// checkout.session.completed, which still pays the order. The card is read from Stripe, replaces the
/// customer's earlier card in that currency, and a second event for the same capture changes nothing. A
/// SetupIntent with no saved card behind it — the Plus subscribe flow's — is not this flow's, and a
/// payment without the tick keeps no card.
/// </summary>
public class SavedCardWebhookTests
{
    private const string WebhookSecret = "whsec_saved_card";
    private const string UserId = "user-webhook-card";
    private const string CurrencyId = "currency-czk";
    private const string TenantId = "tenant-card";
    private const string OrderId = "order-card-paid";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IStripeClientFactory> _stripeFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public SavedCardWebhookTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _processedEvents.Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _stripeFactory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        _stripe.Setup(c => c.GetSetupIntentCardAsync("seti_card_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavedCardDetails("pm_new", "mastercard", "5454", 7, 2031));
        _stripe.Setup(c => c.GetPaymentIntentCardAsync("pi_card_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavedCardDetails("pm_paid", "visa", "4242", 3, 2030));
        _savedCards.Setup(r => r.GetCapturedForUserInCurrencyAsync(UserId, CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private HandlePaymentNotification.Handler Handler() => new(
        _stripeConfig.Object,
        _orders.Object,
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IDisputeRepository>(),
        _processedEvents.Object,
        Mock.Of<IStripeSubscriptionWebhookHandler>(),
        _tenantProvider.Object,
        Mock.Of<IPendingDispatch>(),
        new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        Mock.Of<IUserNotificationRepository>(),
        _stripeFactory.Object,
        Mock.Of<ITenantRepository>(),
        _savedCards.Object,
        Mock.Of<IReceivableRepository>(),
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private SavedCard PendingCard()
    {
        var card = SavedCard.Start(UserId, CurrencyId, "cus_card", "198.51.100.4", "iPhone");
        card.TenantId = TenantId;
        _savedCards.Setup(r => r.GetByIdIgnoringTenantAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        return card;
    }

    private Order ArrangeOrder()
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Cleansia.Core.Domain.Users.Address.Create("Testovaci 12", "Praha", "11000", "country-cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId);
        order.Id = OrderId;
        order.TenantId = "tenant-operator";
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orders.Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult> DeliverAsync(string payload) =>
        Handler().Handle(new HandlePaymentNotification.Command(payload, Sign(payload)), CancellationToken.None);

    [Fact]
    public async Task A_Mobile_Setup_Lands_The_Card_Stripe_Saved_On_Its_Row_Under_The_Rows_Company()
    {
        var card = PendingCard();

        var result = await DeliverAsync(SetupIntentSucceeded("evt_seti_1", card.Id));

        Assert.True(result.IsSuccess);
        Assert.True(card.IsCaptured);
        Assert.Equal(("pm_new", "mastercard", "5454", 7, 2031), (card.StripePaymentMethodId, card.Brand, card.Last4, card.ExpMonth, card.ExpYear));
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
    }

    [Fact]
    public async Task A_Web_Setup_Checkout_Lands_The_Card_Through_Its_SetupIntent()
    {
        var card = PendingCard();

        var result = await DeliverAsync(SetupSessionCompleted("evt_cs_setup_1", card.Id));

        Assert.True(result.IsSuccess);
        Assert.True(card.IsCaptured);
        Assert.Equal("pm_new", card.StripePaymentMethodId);
    }

    [Fact]
    public async Task A_New_Card_Replaces_The_Customers_Earlier_Card_In_That_Currency()
    {
        var card = PendingCard();
        var earlier = SavedCard.Start(UserId, CurrencyId, "cus_card", null, null);
        earlier.Capture("pm_old", "visa", "4242", 1, 2029);
        _savedCards.Setup(r => r.GetCapturedForUserInCurrencyAsync(UserId, CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([earlier]);

        await DeliverAsync(SetupIntentSucceeded("evt_seti_2", card.Id));

        _savedCards.Verify(r => r.Deactivate(earlier), Times.Once);
        _savedCards.Verify(r => r.Deactivate(card), Times.Never);
    }

    [Fact]
    public async Task The_Second_Event_Of_One_Capture_Changes_Nothing()
    {
        var card = PendingCard();
        card.Capture("pm_first", "visa", "4242", 1, 2029);

        var result = await DeliverAsync(SetupIntentSucceeded("evt_seti_3", card.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("pm_first", card.StripePaymentMethodId);
        _stripe.Verify(c => c.GetSetupIntentCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_SetupIntent_With_No_Saved_Card_Behind_It_Is_Ignored()
    {
        var result = await DeliverAsync(SetupIntentSucceeded("evt_seti_plus", savedCardId: null));

        Assert.True(result.IsSuccess);
        _savedCards.Verify(r => r.GetByIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _stripe.Verify(c => c.GetSetupIntentCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Mobile_Payment_That_Kept_The_Card_Lands_It_And_Still_Pays_The_Order()
    {
        var card = PendingCard();
        var order = ArrangeOrder();

        var result = await DeliverAsync(PaymentIntentSucceeded("evt_pi_1", OrderId, card.Id));

        Assert.True(result.IsSuccess);
        Assert.True(card.IsCaptured);
        Assert.Equal(("pm_paid", "visa", "4242", 3, 2030), (card.StripePaymentMethodId, card.Brand, card.Last4, card.ExpMonth, card.ExpYear));
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task A_Web_Checkout_That_Kept_The_Card_Lands_It_Through_Its_PaymentIntent_And_Still_Pays_The_Order()
    {
        var card = PendingCard();
        var order = ArrangeOrder();

        var result = await DeliverAsync(PaymentSessionCompleted("evt_cs_pay_1", OrderId, card.Id));

        Assert.True(result.IsSuccess);
        Assert.True(card.IsCaptured);
        Assert.Equal("pm_paid", card.StripePaymentMethodId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task A_Card_Kept_At_Payment_Replaces_The_Customers_Earlier_Card_In_That_Currency()
    {
        var card = PendingCard();
        ArrangeOrder();
        var earlier = SavedCard.Start(UserId, CurrencyId, "cus_card", null, null);
        earlier.Capture("pm_old", "visa", "4242", 1, 2029);
        _savedCards.Setup(r => r.GetCapturedForUserInCurrencyAsync(UserId, CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([earlier]);

        await DeliverAsync(PaymentIntentSucceeded("evt_pi_2", OrderId, card.Id));

        _savedCards.Verify(r => r.Deactivate(earlier), Times.Once);
        _savedCards.Verify(r => r.Deactivate(card), Times.Never);
    }

    /// <summary>
    /// A web payment raises checkout.session.completed and its PaymentIntent's own payment_intent.succeeded,
    /// which carries the saved card but no order: whichever comes second finds the card captured.
    /// </summary>
    [Fact]
    public async Task The_Second_Event_Of_One_Payment_Changes_Nothing()
    {
        var card = PendingCard();
        card.Capture("pm_first", "visa", "4242", 1, 2029);

        var result = await DeliverAsync(PaymentIntentSucceeded("evt_pi_3", orderId: null, card.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("pm_first", card.StripePaymentMethodId);
        _stripe.Verify(c => c.GetPaymentIntentCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Replayed_Payment_Event_Changes_Nothing()
    {
        var card = PendingCard();
        var order = ArrangeOrder();
        _processedEvents.Setup(r => r.HasProcessedAsync("evt_pi_replayed", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await DeliverAsync(PaymentIntentSucceeded("evt_pi_replayed", OrderId, card.Id));

        Assert.True(result.IsSuccess);
        Assert.False(card.IsCaptured);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        _stripe.Verify(c => c.GetPaymentIntentCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Payment_Without_The_Tick_Keeps_No_Card()
    {
        var order = ArrangeOrder();

        var result = await DeliverAsync(PaymentIntentSucceeded("evt_pi_plain", OrderId, savedCardId: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        _savedCards.Verify(r => r.GetByIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _stripe.Verify(c => c.GetPaymentIntentCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Payment_Whose_Intent_Kept_No_Card_Captures_Nothing_And_Still_Pays_The_Order()
    {
        var card = PendingCard();
        var order = ArrangeOrder();
        _stripe.Setup(c => c.GetPaymentIntentCardAsync("pi_card_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavedCardDetails?)null);

        var result = await DeliverAsync(PaymentIntentSucceeded("evt_pi_nocard", OrderId, card.Id));

        Assert.True(result.IsSuccess);
        Assert.False(card.IsCaptured);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    private static string Metadata(string? savedCardId) =>
        savedCardId is null ? "{}" : $$"""{ "SavedCardId": "{{savedCardId}}" }""";

    private static string PaymentMetadata(string? orderId, string? savedCardId) =>
        "{ " + string.Join(", ", new[]
        {
            orderId is null ? null : $"\"OrderId\": \"{orderId}\"",
            savedCardId is null ? null : $"\"SavedCardId\": \"{savedCardId}\"",
        }.Where(entry => entry is not null)) + " }";

    private static string PaymentIntentSucceeded(string eventId, string? orderId, string? savedCardId) => Event(eventId, Constants.StripeEventType.PaymentIntentSucceeded, $$"""
        {
          "id": "pi_card_1",
          "object": "payment_intent",
          "amount": 150000,
          "currency": "czk",
          "status": "succeeded",
          "setup_future_usage": "off_session",
          "metadata": {{PaymentMetadata(orderId, savedCardId)}}
        }
        """);

    private static string PaymentSessionCompleted(string eventId, string orderId, string savedCardId) => Event(eventId, Constants.StripeEventType.CompletedSession, $$"""
        {
          "id": "cs_test_pay",
          "object": "checkout.session",
          "mode": "payment",
          "payment_status": "paid",
          "payment_intent": "pi_card_1",
          "metadata": {{PaymentMetadata(orderId, savedCardId)}}
        }
        """);

    private static string SetupIntentSucceeded(string eventId, string? savedCardId) => Event(eventId, Constants.StripeEventType.SetupIntentSucceeded, $$"""
        {
          "id": "seti_card_1",
          "object": "setup_intent",
          "status": "succeeded",
          "payment_method": "pm_new",
          "metadata": {{Metadata(savedCardId)}}
        }
        """);

    private static string SetupSessionCompleted(string eventId, string savedCardId) => Event(eventId, Constants.StripeEventType.CompletedSession, $$"""
        {
          "id": "cs_test_setup",
          "object": "checkout.session",
          "mode": "setup",
          "setup_intent": "seti_card_1",
          "metadata": {{Metadata(savedCardId)}}
        }
        """);

    private static string Event(string eventId, string type, string dataObject) => $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{type}}",
          "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": { "object": {{dataObject}}, "previous_attributes": null }
        }
        """;

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={timestamp},v1={EventUtility.ComputeSignature(WebhookSecret, timestamp, payload)}";
    }
}
