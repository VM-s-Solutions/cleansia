using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// What a customer owes on an order is settled through the webhook (owner ruling 2026-09-28, decision 18):
/// a paid pay link or a successful off-session charge marks the receivable paid under its own company and
/// asks for its fee receipt, and never touches the order's sale; a second payment of a receivable already
/// paid is refunded in full; a declined off-session charge, or one the bank wants authenticated, e-mails the
/// customer a pay link for the amount and records it; a receivable no longer open is left alone.
/// </summary>
public class ReceivableWebhookTests
{
    private const string WebhookSecret = "whsec_receivable";
    private const string TenantId = "tenant-receivable";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IStripeClientFactory> _stripeFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Receivable _receivable = OpenReceivable();

    public ReceivableWebhookTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _stripeConfig.SetupGet(c => c.Enabled).Returns(true);
        _processedEvents.Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _stripeFactory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        _stripe
            .Setup(c => c.CreateReceivableCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string receivableId, string? currentSessionId, string orderId, string orderNumber, decimal amount, string currency, CancellationToken ct) =>
                new CheckoutSessionResult($"cs_{receivableId}", $"https://checkout.stripe.test/pay/{receivableId}"));
        _receivables
            .Setup(r => r.GetByIdIgnoringTenantAsync(_receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_receivable);
    }

    private HandlePaymentNotification.Handler Handler() => new(
        _stripeConfig.Object,
        _orders.Object,
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IDisputeRepository>(),
        _processedEvents.Object,
        Mock.Of<IStripeSubscriptionWebhookHandler>(),
        _tenantProvider.Object,
        _pending.Object,
        new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        Mock.Of<IUserNotificationRepository>(),
        _stripeFactory.Object,
        Mock.Of<ITenantRepository>(),
        Mock.Of<ISavedCardRepository>(),
        _receivables.Object,
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private static Receivable OpenReceivable()
    {
        var order = Order.Create(
            customerName: "Owing Customer",
            customerEmail: "owing@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-owing");
        var receivable = Receivable.ForCashCancellationFee(order, 375m);
        receivable.TenantId = TenantId;
        typeof(Receivable).GetProperty(nameof(Receivable.Order))!.SetValue(receivable, order);
        typeof(Receivable).GetProperty(nameof(Receivable.Currency))!.SetValue(receivable, Currency.Create("CZK", "Kč", "Czech koruna"));
        return receivable;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult> DeliverAsync(string payload) =>
        Handler().Handle(new HandlePaymentNotification.Command(payload, Sign(payload)), CancellationToken.None);

    [Fact]
    public async Task A_Paid_Pay_Link_Settles_Its_Receivable_And_Asks_For_Its_Fee_Receipt_Under_Its_Company()
    {
        var result = await DeliverAsync(PayLinkCompleted("evt_link_1", _receivable.Id, "pi_link"));

        Assert.True(result.IsSuccess);
        Assert.True(_receivable.IsPaid);
        Assert.Equal("pi_link", _receivable.StripePaymentIntentId);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
        _pending.Verify(p => p.Enqueue(
            QueueNames.GenerateReceipt,
            It.Is<QueueEnvelope<GenerateReceiptMessage>>(e => e.TenantId == TenantId
                && e.Payload.OrderId == _receivable.OrderId
                && e.Payload.ReceivableId == _receivable.Id),
            MessageKeys.FeeReceipt(_receivable.Id)), Times.Once);
    }

    [Fact]
    public async Task A_Paid_Fee_Receivable_Asks_For_The_Crews_Share_Under_The_Orders_Company()
    {
        var order = _receivable.Order!;
        order.TenantId = TenantId;
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee("emp-owed-share", ContractStatus.Approved)));

        var result = await DeliverAsync(PayLinkCompleted("evt_link_share", _receivable.Id, "pi_share"));

        Assert.True(result.IsSuccess);
        _pending.Verify(p => p.Enqueue(
            QueueNames.CalculateOrderPay,
            It.Is<QueueEnvelope<CalculateOrderPayMessage>>(e => e.TenantId == TenantId
                && e.Payload.OrderId == order.Id && e.Payload.EmployeeId == "emp-owed-share"),
            MessageKeys.Pay(order.Id, "emp-owed-share")), Times.Once);
    }

    [Fact]
    public async Task A_Successful_Off_Session_Charge_Settles_Its_Receivable_And_Leaves_The_Orders_Sale_Alone()
    {
        var result = await DeliverAsync(IntentEvent(
            "evt_pi_ok", Constants.StripeEventType.PaymentIntentSucceeded, "pi_off", "succeeded", _receivable.Id));

        Assert.True(result.IsSuccess);
        Assert.True(_receivable.IsPaid);
        Assert.Equal("pi_off", _receivable.StripePaymentIntentId);
        var order = _receivable.Order!;
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.StripePaymentIntentId);
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Second_Payment_Event_For_A_Paid_Receivable_Changes_Nothing()
    {
        _receivable.MarkPaid("pi_first", DateTimeOffset.UtcNow.AddMinutes(-5));

        var result = await DeliverAsync(PayLinkCompleted("evt_link_2", _receivable.Id, "pi_first"));

        Assert.True(result.IsSuccess);
        Assert.Equal("pi_first", _receivable.StripePaymentIntentId);
        _pending.VerifyNoOtherCalls();
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Second_Payment_Of_A_Paid_Receivable_Is_Refunded_In_Full()
    {
        _receivable.MarkPaid("pi_first", DateTimeOffset.UtcNow.AddMinutes(-5));

        var result = await DeliverAsync(IntentEvent(
            "evt_pi_twice", Constants.StripeEventType.PaymentIntentSucceeded, "pi_second", "succeeded", _receivable.Id));

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.RefundPaymentIntentAsync(
            "pi_second", 375m, $"refund:receivable:{_receivable.Id}:pi_second", It.IsAny<CancellationToken>()), Times.Once);
        _stripe.VerifyNoOtherCalls();
        Assert.Equal("pi_first", _receivable.StripePaymentIntentId);
        _pending.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Charge_The_Bank_Wants_Authenticated_Emails_A_Pay_Link_For_The_Amount()
    {
        _receivable.RecordChargeAttempt();

        var result = await DeliverAsync(IntentEvent(
            "evt_pi_sca", Constants.StripeEventType.PaymentIntentPaymentFailed, "pi_off", "requires_payment_method",
            _receivable.Id, failureCode: "authentication_required"));

        Assert.True(result.IsSuccess);
        Assert.True(_receivable.IsOpen);
        _stripe.Verify(c => c.CreateReceivableCheckoutSessionAsync(
            _receivable.Id, null, _receivable.OrderId, _receivable.Order!.DisplayOrderNumber, 375m, "CZK", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal($"cs_{_receivable.Id}", _receivable.PayLinkSessionId);
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendReceivablePayLinkEmailMessage>>(e => e.TenantId == TenantId
                && e.Payload.ReceivableId == _receivable.Id
                && e.Payload.Attempt == 1
                && e.Payload.PayUrl == $"https://checkout.stripe.test/pay/{_receivable.Id}"),
            MessageKeys.ReceivablePayLinkEmail(_receivable.Id, 1)), Times.Once);
    }

    [Fact]
    public async Task A_Failed_Charge_Of_A_Receivable_Written_Off_Meanwhile_Sends_No_Pay_Link()
    {
        _receivable.WriteOff("admin-1", "Goodwill", DateTimeOffset.UtcNow);

        var result = await DeliverAsync(IntentEvent(
            "evt_pi_late", Constants.StripeEventType.PaymentIntentPaymentFailed, "pi_off", "requires_payment_method",
            _receivable.Id, failureCode: "card_declined"));

        Assert.True(result.IsSuccess);
        _stripe.VerifyNoOtherCalls();
        _pending.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Card_Payments_Switched_Off_A_Failed_Charge_Opens_No_Pay_Link()
    {
        _stripeConfig.SetupGet(c => c.Enabled).Returns(false);

        var result = await DeliverAsync(IntentEvent(
            "evt_pi_off", Constants.StripeEventType.PaymentIntentPaymentFailed, "pi_off", "requires_payment_method",
            _receivable.Id, failureCode: "card_declined"));

        Assert.True(result.IsSuccess);
        Assert.True(_receivable.IsOpen);
        _stripe.VerifyNoOtherCalls();
        _pending.VerifyNoOtherCalls();
    }

    private static string PayLinkCompleted(string eventId, string receivableId, string paymentIntentId) =>
        Event(eventId, Constants.StripeEventType.CompletedSession, $$"""
            {
              "id": "cs_test_pay_link",
              "object": "checkout.session",
              "mode": "payment",
              "payment_status": "paid",
              "payment_intent": "{{paymentIntentId}}",
              "metadata": { "ReceivableId": "{{receivableId}}" }
            }
            """);

    private static string IntentEvent(
        string eventId, string type, string paymentIntentId, string status, string receivableId, string? failureCode = null)
    {
        var lastError = failureCode is null ? "null" : "{ \"type\": \"card_error\", \"code\": \"" + failureCode + "\" }";
        return Event(eventId, type, $$"""
            {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "status": "{{status}}",
              "last_payment_error": {{lastError}},
              "metadata": { "ReceivableId": "{{receivableId}}" }
            }
            """);
    }

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
        return $"t={timestamp},v1={Stripe.EventUtility.ComputeSignature(WebhookSecret, timestamp, payload)}";
    }
}
