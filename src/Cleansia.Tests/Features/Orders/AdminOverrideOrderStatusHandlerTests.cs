using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// AC4 — the admin status-override command. An admin sets a valid <see cref="OrderStatus"/> on a
/// non-terminal order; the change is appended as an <see cref="OrderStatusTrack"/> (history is never
/// rewritten), restricted to allowed forward transitions. An illegal/ambiguous override returns a
/// documented <see cref="BusinessErrorMessage"/> code instead of corrupting the trail. A terminal
/// order (Completed / Cancelled) cannot be overridden.
/// </summary>
public class AdminOverrideOrderStatusHandlerTests
{
    private const string OrderId = "order-admin-override-1";
    private const string AdminUserId = "admin-user";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public AdminOverrideOrderStatusHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminUserId);
    }

    private readonly AuditContext _auditContext = new();

    private AdminOverrideOrderStatus.Handler CreateHandler() =>
        new(_orderRepository.Object, _session.Object, _auditContext);

    private Order ArrangeOrder(params OrderStatus[] history)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "owner-user");
        order.Id = OrderId;
        order.SetCurrency(currency);
        foreach (var status in history)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        }

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    [Fact]
    public async Task Admin_Overrides_Confirmed_To_OnTheWay_Succeeds_AndAppendsTrack()
    {
        var order = ArrangeOrder(OrderStatus.New, OrderStatus.Confirmed);

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.OnTheWay), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.OnTheWay, order.CurrentStatus);
        // History is appended, never rewritten: the prior states remain.
        Assert.Contains(order.OrderStatusHistory, s => s.Status == OrderStatus.New);
        Assert.Contains(order.OrderStatusHistory, s => s.Status == OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Admin_Override_To_Same_Status_Returns_InvalidTransition()
    {
        ArrangeOrder(OrderStatus.Confirmed);

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.Confirmed), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidOrderStatusTransition, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Override_Backwards_Returns_InvalidTransition()
    {
        // OnTheWay -> Confirmed is a backwards/ambiguous override — rejected, history untouched.
        var order = ArrangeOrder(OrderStatus.Confirmed, OrderStatus.OnTheWay);

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.Confirmed), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidOrderStatusTransition, result.Error!.Message);
        Assert.Equal(OrderStatus.OnTheWay, order.CurrentStatus);
    }

    [Fact]
    public async Task Admin_Override_To_Cancelled_Returns_InvalidTransition()
    {
        // Cancellation has its own command (AdminCancelOrder, refund seam); the override path must not
        // be a back-door into the terminal Cancelled state.
        ArrangeOrder(OrderStatus.Confirmed);

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.Cancelled), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidOrderStatusTransition, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Override_On_Completed_Order_Returns_OrderAlreadyCompleted()
    {
        ArrangeOrder(OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed);

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.OnTheWay), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCompleted, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Override_On_Cancelled_Order_Returns_OrderAlreadyCancelled()
    {
        ArrangeOrder(OrderStatus.Confirmed, OrderStatus.Cancelled);

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.OnTheWay), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCancelled, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Override_NotFound_Returns_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(
            new AdminOverrideOrderStatus.Command("missing", OrderStatus.OnTheWay), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }
}
