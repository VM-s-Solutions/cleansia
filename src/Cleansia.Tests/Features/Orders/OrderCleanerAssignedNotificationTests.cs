using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// "A cleaner is on this job" is a claim only an assignment row can make. It used to ride
/// <c>order.confirmed</c> behind <c>TakeOrder</c>'s <c>statusChanged</c> guard, which is exactly
/// inverted for a card order: the Stripe webhook already wrote <see cref="OrderStatus.Confirmed"/>
/// when the payment cleared, so the guard was false at the one moment a cleaner appeared and the
/// customer was told nothing — having been told "Cleaner found!" at card clearance instead.
///
/// <para>These pin the claim to the assignment on EVERY path that creates one, one test per site, so
/// a site regressing cannot hide behind another.</para>
/// </summary>
public class OrderCleanerAssignedNotificationTests
{
    private const string OrderId = "order-assign-1";
    private const string CustomerUserId = "user-customer-1";
    private const string TenantId = "tenant-1";
    private const string TakerEmployeeId = "emp-taker";
    private const string OtherEmployeeId = "emp-other";
    private const string AdminUserId = "admin-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private readonly List<(string UserId, string EventKey, Dictionary<string, string> Args, string? Subject)> _sent = [];

    public OrderCleanerAssignedNotificationTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminUserId);
        _notificationProducer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (userId, eventKey, args, _, subject, _) => _sent.Add((userId, eventKey, args, subject)))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Taking_A_Card_Order_Already_Confirmed_By_The_Webhook_Tells_The_Customer_A_Cleaner_Is_Assigned()
    {
        var order = ArrangeOrder(
            OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 1);
        ArrangeTaker();

        var result = await CreateTakeHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(_sent);
        Assert.Equal(NotificationEventCatalog.OrderCleanerAssigned, sent.EventKey);
        Assert.Equal(CustomerUserId, sent.UserId);
        Assert.Equal(OrderId, sent.Args["orderId"]);
        Assert.Equal(order.DisplayOrderNumber, sent.Args["orderNumber"]);
        Assert.Equal(OrderId, sent.Subject);
    }

    [Fact]
    public async Task Taking_A_Cash_Order_Notifies_Once_And_Still_Confirms_It()
    {
        var order = ArrangeOrder(
            OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, maxEmployees: 1);
        ArrangeTaker();

        var result = await CreateTakeHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        var sent = Assert.Single(_sent);
        Assert.Equal(NotificationEventCatalog.OrderCleanerAssigned, sent.EventKey);
    }

    [Fact]
    public async Task Taking_An_Order_Never_Claims_A_Cleaner_Through_OrderConfirmed()
    {
        ArrangeOrder(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, maxEmployees: 1);
        ArrangeTaker();

        await CreateTakeHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.DoesNotContain(_sent, s => s.EventKey == NotificationEventCatalog.OrderConfirmed);
    }

    [Fact]
    public async Task Taking_A_Guest_Order_Tells_Nobody()
    {
        ArrangeOrder(
            OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 1, userId: null);
        ArrangeTaker();

        var result = await CreateTakeHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task An_Admin_Reassign_Tells_The_Customer_A_Cleaner_Is_Assigned()
    {
        var order = ArrangeOrder(
            OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 1, OtherEmployeeId);
        ArrangeReassignTarget();

        var result = await CreateReassignHandler().Handle(
            new AdminReassignOrder.Command(OrderId, OtherEmployeeId, TakerEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(_sent);
        Assert.Equal(NotificationEventCatalog.OrderCleanerAssigned, sent.EventKey);
        Assert.Equal(CustomerUserId, sent.UserId);
        Assert.Equal(order.DisplayOrderNumber, sent.Args["orderNumber"]);
    }

    [Fact]
    public async Task A_Refused_Admin_Reassign_Tells_Nobody()
    {
        ArrangeOrder(
            OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 1, OtherEmployeeId);
        ArrangeReassignTarget();

        var result = await CreateReassignHandler().Handle(
            new AdminReassignOrder.Command(OrderId, FromEmployeeId: null, TakerEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_sent);
    }

    [Fact]
    public void The_Assignment_Claim_Is_Mutable_Under_Order_Updates()
    {
        Assert.Equal(
            NotificationCategory.OrderUpdates,
            NotificationEventCatalog.GetCategoryFor(NotificationEventCatalog.OrderCleanerAssigned));
    }

    private Order ArrangeOrder(
        OrderStatus currentStatus,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        int maxEmployees,
        string? assignedEmployeeId = null,
        string? userId = CustomerUserId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus,
            userId: userId);
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.SetMaxEmployees(maxEmployees);

        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", DateTimeOffset.UtcNow.AddMinutes(-10));
        order.AddOrderStatus(initial);

        if (currentStatus != OrderStatus.New)
        {
            var latest = OrderStatusTrack.Create(currentStatus, order);
            latest.Created("test", DateTimeOffset.UtcNow);
            order.AddOrderStatus(latest);
        }

        if (assignedEmployeeId is not null)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(order, BuildEmployee(assignedEmployeeId)));
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void ArrangeTaker()
    {
        _employeeRepository
            .Setup(r => r.GetByIdAsync(TakerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmployee(TakerEmployeeId));
        _accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TakerEmployeeId);
    }

    private void ArrangeReassignTarget() =>
        _employeeRepository
            .Setup(r => r.GetByIdAsync(TakerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmployee(TakerEmployeeId));

    private static Employee BuildEmployee(string employeeId)
    {
        var user = User.CreateWithPassword($"{employeeId}@example.com", "x", "Emp", "Loyee");
        user.Id = employeeId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.UpdateContractStatus(ContractStatus.Approved);
        employee.UpdateAddress(Address.Create("123 Main St", "Prague", "11000", "cz"));
        return employee;
    }

    private TakeOrder.Handler CreateTakeHandler() =>
        new(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            _notificationProducer.Object,
            _emailService.Object,
            NullLogger<TakeOrder.Handler>.Instance);

    private AdminReassignOrder.Handler CreateReassignHandler() =>
        new(
            _orderRepository.Object,
            _employeeRepository.Object,
            _session.Object,
            _notificationProducer.Object);
}
