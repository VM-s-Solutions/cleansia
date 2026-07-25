using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Reconciliation safety for the cleaner's cash collection on a CARD order (the Stripe webhook never
/// arrived). Before any cash is recorded the handler asks Stripe what actually happened on the order's
/// charge surface, so the customer is never asked to pay twice:
///   • settled at Stripe    → the order is repaired to Paid and the request is REJECTED
///                            (order.card_payment_already_settled) — the cleaner's next refresh just
///                            shows "complete";
///   • still processing     → REJECTED (order.card_payment_in_progress);
///   • Stripe unreachable   → REJECTED (order.card_payment_unverified) — deliberately fail closed, the
///                            admin status-override is the release valve;
///   • confirmed unpaid     → the outstanding intent is best-effort cancelled, then the cash is recorded.
/// A cash-booked order has no Stripe surface and never round-trips.
/// PaymentType stays as booked (Card) — the derived <see cref="Order.ActualPaymentType"/> carries the
/// tender that was actually taken.
/// </summary>
public class MarkCashCollectedHandlerTests
{
    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";
    private const string PaymentIntentId = "pi_test_1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IStripeClient> _stripeClient = new();

    public MarkCashCollectedHandlerTests()
    {
        _accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);
    }

    private MarkCashCollected.Handler CreateHandler() => new(
        _orderRepository.Object,
        _accessService.Object,
        _stripeClient.Object,
        NullLogger<MarkCashCollected.Handler>.Instance);

    private Order ArrangeOrder(PaymentType paymentType, bool withStripeSurface)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, EmployeeId, paymentType, PaymentStatus.Pending);

        if (withStripeSurface)
        {
            order.AssignStripePaymentIntentId(PaymentIntentId);
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void ArrangeStripeState(StripePaymentState state, string? outstandingIntentId = null)
    {
        _stripeClient
            .Setup(c => c.GetPaymentSnapshotAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripePaymentSnapshot(state, outstandingIntentId));
    }

    [Fact]
    public async Task Cash_Order_Without_Stripe_Surface_Records_Collection_Without_Asking_Stripe()
    {
        var order = ArrangeOrder(PaymentType.Cash, withStripeSurface: false);

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.NotNull(order.CashCollectedAt);
        Assert.Equal(EmployeeId, order.CollectedByEmployeeId);
        _stripeClient.Verify(
            c => c.GetPaymentSnapshotAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Card_Order_Already_Settled_At_Stripe_Repairs_To_Paid_And_Rejects()
    {
        var order = ArrangeOrder(PaymentType.Card, withStripeSurface: true);
        ArrangeStripeState(StripePaymentState.Settled, PaymentIntentId);

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CardPaymentAlreadySettled, result.Error!.Message);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Null(order.CashCollectedAt);
        Assert.Equal(PaymentType.Card, order.ActualPaymentType);
        // The pipeline does not commit a FAILED command, so the repair must be flushed by the handler
        // or the cleaner refreshes into the same dead end forever.
        _orderRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _stripeClient.Verify(
            c => c.CancelPaymentIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Card_Order_Payment_Still_Processing_Rejects_And_Writes_Nothing()
    {
        var order = ArrangeOrder(PaymentType.Card, withStripeSurface: true);
        ArrangeStripeState(StripePaymentState.Processing, PaymentIntentId);

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CardPaymentInProgress, result.Error!.Message);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.CashCollectedAt);
        _orderRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _stripeClient.Verify(
            c => c.CancelPaymentIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(StripeReadFailures))]
    public async Task Card_Order_With_Unreachable_Stripe_Fails_Closed(Exception failure)
    {
        var order = ArrangeOrder(PaymentType.Card, withStripeSurface: true);
        _stripeClient
            .Setup(c => c.GetPaymentSnapshotAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CardPaymentUnverified, result.Error!.Message);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.CashCollectedAt);
        _orderRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<Exception> StripeReadFailures() =>
    [
        new StripeException("stripe is down"),
        new HttpRequestException("connection reset"),
        new TimeoutException("gateway timeout"),
    ];

    [Fact]
    public async Task Card_Order_Confirmed_Unpaid_Cancels_Outstanding_Intent_Then_Records_Cash()
    {
        var order = ArrangeOrder(PaymentType.Card, withStripeSurface: true);
        ArrangeStripeState(StripePaymentState.Unpaid, PaymentIntentId);

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.NotNull(order.CashCollectedAt);
        _stripeClient.Verify(c => c.CancelPaymentIntentAsync(PaymentIntentId, It.IsAny<CancellationToken>()), Times.Once);
        // The booking contract is untouched — the refund path still resolves the Stripe surface by
        // PaymentType.Card — while the derived tender reports what the customer actually handed over.
        Assert.Equal(PaymentType.Card, order.PaymentType);
        Assert.Equal(PaymentType.Cash, order.ActualPaymentType);
    }

    [Fact]
    public async Task Card_Order_Confirmed_Unpaid_Records_Cash_Even_When_The_Cancel_Fails()
    {
        var order = ArrangeOrder(PaymentType.Card, withStripeSurface: true);
        ArrangeStripeState(StripePaymentState.Unpaid, PaymentIntentId);
        _stripeClient
            .Setup(c => c.CancelPaymentIntentAsync(PaymentIntentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("intent already canceled"));

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.NotNull(order.CashCollectedAt);
    }

    [Fact]
    public async Task Card_Order_Confirmed_Unpaid_With_No_Outstanding_Intent_Records_Cash()
    {
        var order = ArrangeOrder(PaymentType.Card, withStripeSurface: true);
        ArrangeStripeState(StripePaymentState.Unpaid, outstandingIntentId: null);

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(order.CashCollectedAt);
        _stripeClient.Verify(
            c => c.CancelPaymentIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Order_Not_Found_Returns_OrderNotFound()
    {
        _orderRepository.Setup(r => r.GetQueryable()).Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }
}
