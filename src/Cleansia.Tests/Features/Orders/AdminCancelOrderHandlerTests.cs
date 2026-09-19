using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The admin-cancel command after the money path moved to <see cref="IPlatformOrderCancellation"/>
/// (ADR-0064 D2): the handler loads the order, keeps its terminal-state gates with the existing
/// <see cref="BusinessErrorMessage"/> codes, has no ownership gate (an admin cancels ANY order), and
/// hands the body one call with <see cref="CancelledBy.Admin"/>, the admin's note and
/// <see cref="RefundReason.CustomerCancellation"/> — the reason that keeps its refund key
/// <c>refund:{id}:cancel</c>, byte-identical to before the extraction. The body's own guarantees are
/// <c>PlatformOrderCancellationTests</c>.
/// </summary>
public class AdminCancelOrderHandlerTests
{
    private const string OrderId = "order-admin-cancel-1";
    private const string OwnerUserId = "owner-user";
    private const string AdminUserId = "admin-user";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPlatformOrderCancellation> _cancellation = new();

    public AdminCancelOrderHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminUserId);
        _cancellation
            .Setup(c => c.CancelAsync(
                It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancelledBy>(), It.IsAny<string?>(),
                It.IsAny<RefundReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order o, string _, CancelledBy by, string? reason, RefundReason _, CancellationToken _) =>
            {
                o.Cancel(DateTime.UtcNow, by, 0m, o.TotalPrice, reason);
                o.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, o));
                return new PlatformOrderCancellationResult(o.TotalPrice, PlatformRefundOutcome.Issued);
            });
    }

    private AdminCancelOrder.Handler CreateHandler() =>
        new(_orderRepository.Object, _session.Object, _cancellation.Object);

    private Order ArrangeOrder(OrderStatus latestStatus)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            // OWNED BY A DIFFERENT USER — proves the admin path has no ownership gate.
            userId: OwnerUserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_admin_cancel");
        order.AddOrderStatus(OrderStatusTrack.Create(latestStatus, order));

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    [Fact]
    public async Task Admin_Cancels_NonOwned_Confirmed_Order_Succeeds_AttributedToAdmin()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, "incident"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CancelledBy.Admin, order.CancelledBy);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(1000m, result.Value!.RefundAmount);
        Assert.True(result.Value.RefundInitiated);
    }

    [Fact]
    public async Task Admin_Cancel_Hands_The_Body_The_Admin_The_Note_And_The_CustomerCancellation_Reason()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);

        await CreateHandler().Handle(new AdminCancelOrder.Command(OrderId, "incident"), CancellationToken.None);

        _cancellation.Verify(c => c.CancelAsync(
            order, AdminUserId, CancelledBy.Admin, "incident", RefundReason.CustomerCancellation, It.IsAny<CancellationToken>()),
            Times.Once);
        _cancellation.Verify(c => c.RefundAsync(
            It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<RefundReason>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_Cancel_Reports_A_Refund_The_Body_Could_Not_Issue()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        _cancellation
            .Setup(c => c.CancelAsync(
                order, AdminUserId, CancelledBy.Admin, null, RefundReason.CustomerCancellation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformOrderCancellationResult(1000m, PlatformRefundOutcome.Failed(BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RefundInitiated);
    }

    [Theory]
    [InlineData(OrderStatus.Completed, BusinessErrorMessage.OrderAlreadyCompleted)]
    [InlineData(OrderStatus.Cancelled, BusinessErrorMessage.OrderAlreadyCancelled)]
    [InlineData(OrderStatus.InProgress, BusinessErrorMessage.OrderInProgressCannotCancel)]
    public async Task Admin_Cancel_Of_A_Terminal_Or_Running_Order_Is_Refused_Before_The_Body_Runs(
        OrderStatus latestStatus, string expectedError)
    {
        ArrangeOrder(latestStatus);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error!.Message);
        _cancellation.Verify(c => c.CancelAsync(
            It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancelledBy>(), It.IsAny<string?>(),
            It.IsAny<RefundReason>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_Cancel_NotFound_Returns_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command("missing", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }
}
