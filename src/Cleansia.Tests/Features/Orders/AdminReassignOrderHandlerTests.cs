using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// AC5 — the admin reassign command. An admin replaces an order's assigned cleaner with a target
/// cleaner, respecting <c>MaxEmployees</c> / <c>AvailableSpots</c>. A pure add onto a full order
/// surfaces the existing <see cref="BusinessErrorMessage.NoAvailableSpots"/> business error (never an
/// unhandled <see cref="InvalidOperationException"/> from <c>AddAssignedEmployee</c>).
/// </summary>
public class AdminReassignOrderHandlerTests
{
    private const string OrderId = "order-admin-reassign-1";
    private const string AdminUserId = "admin-user";
    private const string FromEmployeeId = "emp-from";
    private const string ToEmployeeId = "emp-to";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public AdminReassignOrderHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminUserId);
    }

    private AdminReassignOrder.Handler CreateHandler() =>
        new(_orderRepository.Object, _employeeRepository.Object, _session.Object);

    private static Employee BuildEmployee(string employeeId)
    {
        var user = User.CreateWithPassword(employeeId + "@x.test", "x", "Emp", "Loyee");
        user.Id = employeeId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }

    private Order ArrangeOrder(int maxEmployees, params string[] assignedEmployeeIds)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "owner-user");
        order.Id = OrderId;
        order.SetMaxEmployees(maxEmployees);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        foreach (var id in assignedEmployeeIds)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(order, BuildEmployee(id)));
        }

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void ArrangeTargetEmployeeExists(string employeeId)
    {
        _employeeRepository
            .Setup(r => r.GetByIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmployee(employeeId));
    }

    [Fact]
    public async Task Admin_Reassign_Replaces_From_With_Target_Succeeds()
    {
        var order = ArrangeOrder(maxEmployees: 1, FromEmployeeId);
        ArrangeTargetEmployeeExists(ToEmployeeId);

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, FromEmployeeId, ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(order.AssignedEmployees, oe => oe.EmployeeId == FromEmployeeId);
        Assert.Contains(order.AssignedEmployees, oe => oe.EmployeeId == ToEmployeeId);
    }

    [Fact]
    public async Task Admin_Reassign_PureAdd_To_OrderWithOpenSpot_Succeeds()
    {
        // maxEmployees=2, one assigned, no FromEmployeeId => a pure add into the open spot.
        var order = ArrangeOrder(maxEmployees: 2, FromEmployeeId);
        ArrangeTargetEmployeeExists(ToEmployeeId);

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, FromEmployeeId: null, ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(order.AssignedEmployees, oe => oe.EmployeeId == FromEmployeeId);
        Assert.Contains(order.AssignedEmployees, oe => oe.EmployeeId == ToEmployeeId);
    }

    [Fact]
    public async Task Admin_Reassign_PureAdd_To_FullOrder_Returns_NoAvailableSpots()
    {
        // maxEmployees=1, full, no FromEmployeeId => exceeds MaxEmployees: business error, not an exception.
        ArrangeOrder(maxEmployees: 1, FromEmployeeId);
        ArrangeTargetEmployeeExists(ToEmployeeId);

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, FromEmployeeId: null, ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NoAvailableSpots, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Reassign_TargetAlreadyAssigned_Returns_EmployeeAlreadyAssignedToOrder()
    {
        ArrangeOrder(maxEmployees: 2, FromEmployeeId, ToEmployeeId);
        ArrangeTargetEmployeeExists(ToEmployeeId);

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, FromEmployeeId, ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeAlreadyAssignedToOrder, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Reassign_FromNotAssigned_Returns_EmployeeNotAssignedToOrder()
    {
        ArrangeOrder(maxEmployees: 2, FromEmployeeId);
        ArrangeTargetEmployeeExists(ToEmployeeId);

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, "emp-not-on-order", ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeNotAssignedToOrder, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Reassign_TargetEmployeeMissing_Returns_EmployeeNotFound()
    {
        ArrangeOrder(maxEmployees: 1, FromEmployeeId);
        _employeeRepository
            .Setup(r => r.GetByIdAsync(ToEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command(OrderId, FromEmployeeId, ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Reassign_OrderNotFound_Returns_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(
            new AdminReassignOrder.Command("missing", FromEmployeeId, ToEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }
}
