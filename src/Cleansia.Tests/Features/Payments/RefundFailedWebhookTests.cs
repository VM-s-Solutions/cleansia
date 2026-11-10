using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// Stripe's refund.failed: a card refund that failed after Stripe accepted it. The row is found by the
/// RefundKey metadata under its own company. A Succeeded row stays Succeeded, because the money still counts
/// as refunded (D23), and a Pending row turns Failed; both get the failed date and Stripe's refund id, and
/// the one delivery that sets the date tells the company's administrators once. A key no order refund
/// carries, a receivable's own refund key and a redelivered event change nothing.
/// </summary>
public class RefundFailedWebhookTests
{
    private const string WebhookSecret = "whsec_refund_failed";
    private const string TenantId = "tenant-refund-failed";
    private const string RefundKey = "refund:order-refund-failed:admin:full";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly List<AdminEvent> _raised = [];
    private readonly Order _order = PaidCardOrder();

    public RefundFailedWebhookTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _stripeConfig.SetupGet(c => c.Enabled).Returns(true);
        _processedEvents.Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((adminEvent, _) => _raised.Add(adminEvent))
            .Returns(Task.CompletedTask);
    }

    private HandlePaymentNotification.Handler Handler() => new(
        _stripeConfig.Object,
        Mock.Of<IOrderRepository>(),
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IDisputeRepository>(),
        _processedEvents.Object,
        Mock.Of<IStripeSubscriptionWebhookHandler>(),
        _tenantProvider.Object,
        Mock.Of<IPendingDispatch>(),
        new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        _adminNotifier.Object,
        Mock.Of<IUserNotificationRepository>(),
        Mock.Of<IStripeClientFactory>(),
        Mock.Of<ITenantRepository>(),
        Mock.Of<ISavedCardRepository>(),
        Mock.Of<IReceivableRepository>(),
        _refunds.Object,
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private static Order PaidCardOrder()
    {
        var order = Order.Create(
            customerName: "Refunded Customer",
            customerEmail: "refunded@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Refunded,
            userId: "user-refunded",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = "order-refund-failed";
        order.TenantId = TenantId;
        return order;
    }

    private Refund OnFile(RefundStatus status, string refundKey = RefundKey)
    {
        var refund = Refund.Create(_order.Id, refundKey, 1000m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        refund.TenantId = TenantId;
        typeof(Refund).GetProperty(nameof(Refund.Order))!.SetValue(refund, _order);
        if (status == RefundStatus.Succeeded)
        {
            refund.MarkSucceeded("re_sent", DateTimeOffset.UtcNow.AddDays(-1));
        }

        _refunds.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { refund }.AsQueryable().BuildMock());
        return refund;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult> DeliverAsync(string payload) =>
        Handler().Handle(new HandlePaymentNotification.Command(payload, Sign(payload)), CancellationToken.None);

    [Fact]
    public async Task A_Bounced_Refund_Stays_Succeeded_Gets_Its_Failed_Date_And_Stripe_Id_And_Tells_The_Administrators_Once()
    {
        var refund = OnFile(RefundStatus.Succeeded);

        var result = await DeliverAsync(RefundFailed("evt_bounced", "re_bounced", RefundKey));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.NotNull(refund.FailedOn);
        Assert.Equal("re_bounced", refund.StripeRefundId);
        Assert.Equal(PaymentStatus.Refunded, _order.PaymentStatus);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
        var alert = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.RefundBounced, alert.Key);
        Assert.Equal(TenantId, alert.TenantId);
        Assert.Equal(refund.Id, alert.Subject);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["orderNumber"] = _order.DisplayOrderNumber,
                ["amount"] = "1000 CZK",
                ["orderId"] = _order.Id,
            },
            alert.Args);
    }

    [Fact]
    public async Task A_Pending_Refund_Stripe_Failed_Turns_Failed_With_Its_Failed_Date()
    {
        var refund = OnFile(RefundStatus.Pending);

        var result = await DeliverAsync(RefundFailed("evt_pending_failed", "re_pending", RefundKey));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RefundStatus.Failed, refund.Status);
        Assert.NotNull(refund.FailedOn);
        Assert.Equal("re_pending", refund.StripeRefundId);
        Assert.Equal(AdminNotificationEventCatalog.RefundBounced, Assert.Single(_raised).Key);
    }

    [Fact]
    public async Task A_Refund_A_Retry_Already_Marked_Raises_Nothing_More()
    {
        var refund = OnFile(RefundStatus.Pending);
        var markedOn = DateTimeOffset.UtcNow.AddMinutes(-5);
        refund.MarkFailedByStripe("re_found", markedOn);

        var result = await DeliverAsync(RefundFailed("evt_after_retry", "re_found", RefundKey));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RefundStatus.Failed, refund.Status);
        Assert.Equal(markedOn, refund.FailedOn);
        Assert.Empty(_raised);
    }

    [Theory]
    [InlineData("refund:order-nobody-knows:admin:full")]
    [InlineData("refund:receivable:rcv-1:pi_second")]
    [InlineData(null)]
    public async Task A_Key_No_Order_Refund_Carries_Changes_Nothing(string? refundKey)
    {
        var refund = OnFile(RefundStatus.Succeeded);

        var result = await DeliverAsync(RefundFailed("evt_unknown_key", "re_unknown", refundKey));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Null(refund.FailedOn);
        Assert.Equal("re_sent", refund.StripeRefundId);
        Assert.Empty(_raised);
        _tenantProvider.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Redelivered_Event_Changes_Nothing()
    {
        var refund = OnFile(RefundStatus.Succeeded);
        _processedEvents.Setup(r => r.HasProcessedAsync("evt_redelivered", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await DeliverAsync(RefundFailed("evt_redelivered", "re_bounced", RefundKey));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(refund.FailedOn);
        Assert.Empty(_raised);
    }

    private static string RefundFailed(string eventId, string stripeRefundId, string? refundKey)
    {
        var metadata = refundKey is null ? "{}" : $$"""{ "RefundKey": "{{refundKey}}" }""";
        return $$"""
            {
              "id": "{{eventId}}",
              "object": "event",
              "api_version": "2024-06-20",
              "type": "{{Constants.StripeEventType.RefundFailed}}",
              "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
              "livemode": false,
              "pending_webhooks": 0,
              "request": null,
              "data": {
                "object": {
                  "id": "{{stripeRefundId}}",
                  "object": "refund",
                  "amount": 100000,
                  "currency": "czk",
                  "status": "failed",
                  "failure_reason": "expired_or_canceled_card",
                  "metadata": {{metadata}}
                },
                "previous_attributes": null
              }
            }
            """;
    }

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={timestamp},v1={Stripe.EventUtility.ComputeSignature(WebhookSecret, timestamp, payload)}";
    }
}
