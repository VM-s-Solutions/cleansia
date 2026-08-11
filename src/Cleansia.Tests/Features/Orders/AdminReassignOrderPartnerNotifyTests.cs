using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The admin reassign is the ONE path where a cleaner's commitments change without the cleaner acting:
/// every other assignment write is <c>TakeOrder</c>, where the cleaner is the actor and already has the
/// command's response. So it is also the only path that owes both cleaners a notice — the one who
/// gained a job they never asked for, and the one whose day just lost the job they had planned around.
/// Before this, both learned nothing; only the customer was told.
///
/// <para>The two events are pinned SEPARATELY in both directions — each cleaner gets exactly their own
/// event and never the other's — because a single "somebody was notified" assertion passes just as well
/// against an implementation that sends the wrong one to the wrong person.</para>
/// </summary>
public class AdminReassignOrderPartnerNotifyTests
{
    private const string OrderId = "order-admin-reassign-notify";
    private const string CustomerUserId = "customer-user";
    private const string FromEmployeeId = "emp-from";
    private const string ToEmployeeId = "emp-to";
    private const string FromUserId = FromEmployeeId + "-user";
    private const string ToUserId = ToEmployeeId + "-user";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<INotificationProducer> _producer = new();

    public AdminReassignOrderPartnerNotifyTests() => _session.Setup(s => s.GetUserId()).Returns("admin-user");

    private AdminReassignOrder.Handler CreateHandler() =>
        new(_orderRepository.Object, _employeeRepository.Object, _session.Object, _producer.Object);

    [Fact]
    public async Task Assigning_A_Cleaner_Tells_That_Cleaner_With_The_Order_Args()
    {
        var order = Arrange(maxEmployees: 1, FromEmployeeId);

        var result = await Reassign(FromEmployeeId);

        Assert.True(result.IsSuccess);
        _producer.Verify(p => p.NotifyAsync(
            ToUserId,
            NotificationEventCatalog.OrderAssigned,
            It.Is<Dictionary<string, string>>(d =>
                d["orderId"] == OrderId && d["orderNumber"] == order.DisplayOrderNumber),
            order.TenantId,
            OrderId,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Replacing_A_Cleaner_Tells_The_REMOVED_Cleaner_The_Job_Is_Gone()
    {
        var order = Arrange(maxEmployees: 1, FromEmployeeId);

        var result = await Reassign(FromEmployeeId);

        Assert.True(result.IsSuccess);
        _producer.Verify(p => p.NotifyAsync(
            FromUserId,
            NotificationEventCatalog.OrderAssignmentRevoked,
            It.Is<Dictionary<string, string>>(d =>
                d["orderId"] == OrderId && d["orderNumber"] == order.DisplayOrderNumber),
            order.TenantId,
            OrderId,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The two events must not be able to stand in for each other: the incoming cleaner is never told a
    /// job was taken away, and the outgoing cleaner is never told they were given one.
    /// </summary>
    [Fact]
    public async Task Neither_Cleaner_Receives_The_Other_Cleaners_Event()
    {
        Arrange(maxEmployees: 1, FromEmployeeId);

        await Reassign(FromEmployeeId);

        VerifyNever(ToUserId, NotificationEventCatalog.OrderAssignmentRevoked);
        VerifyNever(FromUserId, NotificationEventCatalog.OrderAssigned);
    }

    /// <summary>
    /// Both partner events are addressed to a cleaner's user, never the order's. The customer keeps
    /// exactly the one assignment notice they already had.
    /// </summary>
    [Fact]
    public async Task The_Customer_Receives_Neither_Partner_Event_And_Keeps_Their_Own()
    {
        Arrange(maxEmployees: 1, FromEmployeeId);

        await Reassign(FromEmployeeId);

        VerifyNever(CustomerUserId, NotificationEventCatalog.OrderAssigned);
        VerifyNever(CustomerUserId, NotificationEventCatalog.OrderAssignmentRevoked);
        _producer.Verify(p => p.NotifyAsync(
            CustomerUserId,
            NotificationEventCatalog.OrderCleanerAssigned,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A pure add into an open seat removes nobody, so there is nobody to tell a job was lost. Without
    /// this, an implementation that fired the revocation unconditionally would still pass every
    /// assertion above.
    /// </summary>
    [Fact]
    public async Task A_Pure_Add_Into_An_Open_Seat_Revokes_Nobody()
    {
        Arrange(maxEmployees: 2, FromEmployeeId);

        var result = await Reassign(fromEmployeeId: null);

        Assert.True(result.IsSuccess);
        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(),
            NotificationEventCatalog.OrderAssignmentRevoked,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The seat ceiling is refused after the in-memory unassign, and the pipeline does not commit a
    /// failed command — so a cleaner must not be told about a reassignment the database never saw.
    /// </summary>
    [Fact]
    public async Task A_Refused_Reassignment_Notifies_No_One()
    {
        Arrange(maxEmployees: 1, FromEmployeeId);

        var result = await Reassign(fromEmployeeId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NoAvailableSpots, result.Error!.Message);
        _producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Cleaner_With_No_Linked_User_Is_Skipped_Rather_Than_Throwing()
    {
        Arrange(maxEmployees: 1, FromEmployeeId, employeesWithoutUser: [FromEmployeeId, ToEmployeeId]);

        var result = await Reassign(FromEmployeeId);

        Assert.True(result.IsSuccess);
        VerifyNever(string.Empty, NotificationEventCatalog.OrderAssigned);
        VerifyNever(string.Empty, NotificationEventCatalog.OrderAssignmentRevoked);
    }

    private Task<BusinessResult<AdminReassignOrder.Response>> Reassign(string? fromEmployeeId) =>
        CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, fromEmployeeId, ToEmployeeId), CancellationToken.None);

    private void VerifyNever(string userId, string eventKey) =>
        _producer.Verify(p => p.NotifyAsync(
            userId,
            eventKey,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

    private Order Arrange(
        int maxEmployees,
        string assignedEmployeeId,
        string[]? employeesWithoutUser = null)
    {
        var withoutUser = employeesWithoutUser ?? [];

        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId);
        order.Id = OrderId;
        order.SetMaxEmployees(maxEmployees);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.AddAssignedEmployee(
            OrderEmployee.Create(order, BuildEmployee(assignedEmployeeId, withoutUser.Contains(assignedEmployeeId))));

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        foreach (var employeeId in new[] { FromEmployeeId, ToEmployeeId })
        {
            var employee = BuildEmployee(employeeId, withoutUser.Contains(employeeId));
            _employeeRepository
                .Setup(r => r.GetByIdAsync(employeeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(employee);
        }

        return order;
    }

    private static Employee BuildEmployee(string employeeId, bool withoutUser = false)
    {
        var user = User.CreateWithPassword(employeeId + "@x.test", "x", "Emp", "Loyee");
        user.Id = withoutUser ? string.Empty : employeeId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }
}
