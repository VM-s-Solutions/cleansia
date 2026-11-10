using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// An administrator's retry of one refund Stripe has not confirmed, through the real refund seam with a Stripe
/// fake: it asks Stripe first and sends on the same key only when Stripe has nothing, and it is refused for a
/// refund that is not Not sent, a dispute's, or a cancellation's on an order still open.
/// </summary>
public sealed class RetryRefundHandlerTests
{
    private const string OrderId = "order-retry";
    private const string AdminId = "admin-retry";

    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<ILoyaltyService> _loyalty = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();

    private RetryRefund.Handler CreateHandler()
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        var service = new RefundService(
            _refunds.Object, _orders.Object, _credit.Object, factory.Object,
            new Mock<IAdminNotifier>().Object, new Mock<IUserNotificationRepository>().Object,
            NullLogger<RefundService>.Instance);
        return new RetryRefund.Handler(
            _refunds.Object, _orders.Object, service, _loyalty.Object,
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
            _notifications.Object, _outbox.Object, NullLogger<RetryRefund.Handler>.Instance);
    }

    private Order ArrangeOrder(OrderStatus status = OrderStatus.New, string? userId = "user-1")
    {
        var order = OrderMockFactory.GenerateWithStatusHistory([status], totalPrice: 1000m, orderId: OrderId, userId: userId!);
        _orders.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private Refund ArrangeRefund(
        Order order, decimal amount = 400m, RefundReason reason = RefundReason.AdminDiscretion,
        string? requestId = "lines-1", string? disputeId = null)
    {
        var refund = Refund.Create(
            order.Id,
            RefundService.BuildRefundKey(new RefundRequest(order.Id, amount, reason, AdminId, disputeId, requestId)),
            amount, "CZK", reason, RefundSource.AppRefund, disputeId: disputeId);
        _refunds.Setup(r => r.GetByIdAsync(refund.Id, It.IsAny<CancellationToken>())).ReturnsAsync(refund);
        return refund;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<RetryRefund.Response>> RetryAsync(Refund refund, string orderId = OrderId) =>
        CreateHandler().Handle(new RetryRefund.Command(orderId, refund.Id), CancellationToken.None);

    private void VerifySent(Times times) =>
        _stripe.Verify(s => s.RefundCheckoutSessionAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task A_Not_Sent_Partial_Refund_Stripe_Now_Accepts_Turns_Refunded_Moves_The_Payment_Status_And_Claws_Back_Once()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);

        var result = await RetryAsync(refund);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RefundState.Refunded), result.Value!.State.Name);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
        _stripe.Verify(s => s.RefundCheckoutSessionAsync(It.IsAny<string>(), 400m, refund.RefundKey, It.IsAny<CancellationToken>()), Times.Once);
        _loyalty.Verify(l => l.RevokeForRefundAsync(OrderId, 400m, refund.RefundKey, AdminId, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Refund_Stripe_Already_Paid_On_Its_Key_Is_Recorded_And_Nothing_Is_Sent()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);
        _stripe.Setup(s => s.FindRefundAsync(It.IsAny<string?>(), It.IsAny<string?>(), refund.RefundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeRefundSnapshot("re_paid", 400m, Failed: false));

        var result = await RetryAsync(refund);

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal("re_paid", refund.StripeRefundId);
        VerifySent(Times.Never());
    }

    [Fact]
    public async Task A_Refund_Stripe_Failed_On_Its_Key_Turns_Failed_With_Its_Failed_Date()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);
        _stripe.Setup(s => s.FindRefundAsync(It.IsAny<string?>(), It.IsAny<string?>(), refund.RefundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeRefundSnapshot("re_dead", 400m, Failed: true));

        var result = await RetryAsync(refund);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RefundState.Failed), result.Value!.State.Name);
        Assert.Equal(RefundStatus.Failed, refund.Status);
        Assert.NotNull(refund.FailedOn);
        VerifySent(Times.Never());
        _loyalty.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Refund_Stripe_Refuses_Again_Stays_Not_Sent_And_Fails_With_Refund_Failed()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);
        _stripe.Setup(s => s.RefundCheckoutSessionAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("card declined"));

        var result = await RetryAsync(refund);

        Assert.Equal(BusinessErrorMessage.RefundFailed, result.Error!.Message);
        Assert.Equal(RefundStatus.Pending, refund.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        _loyalty.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Stripe_Timeout_Fails_With_Refund_Failed_And_Changes_Nothing()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);
        _stripe.Setup(s => s.RefundCheckoutSessionAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());

        var result = await RetryAsync(refund);

        Assert.Equal(BusinessErrorMessage.RefundFailed, result.Error!.Message);
        Assert.Equal(RefundStatus.Pending, refund.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task A_Failed_Refunded_Or_Closed_Refund_A_Disputes_And_A_Guest_Cancellation_On_An_Open_Order_Are_Not_Retryable()
    {
        var order = ArrangeOrder();
        var failed = ArrangeRefund(order, requestId: "f");
        failed.MarkFailedByStripe("re_f", DateTimeOffset.UtcNow);
        var refunded = ArrangeRefund(order, requestId: "r").MarkSucceeded("re_r", DateTimeOffset.UtcNow);
        var closed = ArrangeRefund(order, requestId: "c").MarkFailed();
        var dispute = ArrangeRefund(order, reason: RefundReason.DisputeResolution, requestId: null, disputeId: "dispute-1");
        var guestCancel = ArrangeRefund(order, reason: RefundReason.CustomerCancellation, requestId: null);

        foreach (var refund in new[] { failed, refunded, closed, dispute, guestCancel })
        {
            Assert.Equal(BusinessErrorMessage.RefundNotRetryable, (await RetryAsync(refund)).Error!.Message);
        }

        VerifySent(Times.Never());
    }

    [Fact]
    public async Task A_Refund_On_Another_Order_Is_Not_Found()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);

        var result = await RetryAsync(refund, orderId: "another-order");

        Assert.Equal(BusinessErrorMessage.RefundNotFound, result.Error!.Message);
        VerifySent(Times.Never());
    }

    [Fact]
    public async Task A_Double_Submit_Sends_Once()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order);

        var first = await RetryAsync(refund);
        var second = await RetryAsync(refund);

        Assert.True(first.IsSuccess);
        Assert.Equal(BusinessErrorMessage.RefundNotRetryable, second.Error!.Message);
        VerifySent(Times.Once());
    }

    [Fact]
    public async Task A_Full_Refund_Tells_The_Customer_And_Claws_Back_The_Whole_Price()
    {
        var order = ArrangeOrder();
        var refund = ArrangeRefund(order, amount: 1000m, requestId: "full");

        var result = await RetryAsync(refund);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        _notifications.Verify(n => n.NotifyAsync(
            "user-1", NotificationEventCatalog.OrderRefunded, It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(),
            refund.Id, It.IsAny<CancellationToken>()), Times.Once);
        _loyalty.Verify(l => l.RevokeForRefundAsync(OrderId, 1000m, refund.RefundKey, AdminId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Cancellation_Refund_On_A_Cancelled_Order_Tells_The_Customer_Once_And_Takes_No_Points_Again()
    {
        var order = ArrangeOrder(OrderStatus.Cancelled);
        var refund = ArrangeRefund(order, amount: 1000m, reason: RefundReason.CustomerCancellation, requestId: null);

        var result = await RetryAsync(refund);

        Assert.True(result.IsSuccess);
        _notifications.Verify(n => n.NotifyAsync(
            "user-1", NotificationEventCatalog.OrderRefunded, It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(),
            refund.Id, It.IsAny<CancellationToken>()), Times.Once);
        _loyalty.VerifyNoOtherCalls();
    }
}
