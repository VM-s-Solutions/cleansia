using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// Rejecting a cleaner has to take back their SEATS, not only their contract.
///
/// <para><b>The defect this closes.</b> Nothing anywhere filters <c>AssignedEmployees</c> by contract
/// status. Offerability counts the row — <c>OrderSpecification.cs:141</c> admits an order only while
/// <c>AssignedEmployees.Count &lt; MaxEmployees</c>, and <c>OrderVisibility.cs:55</c> reads any
/// assignment as taken — while <c>TakeOrder</c>, <c>StartOrder</c>, <c>CompleteOrder</c> and
/// <c>MarkCashCollected</c> every one require <c>ContractStatus.Approved</c>. So a rejected cleaner's
/// row held the job off the board AND could not be worked by the one person holding it. The order was
/// stranded — un-takeable and un-startable — with nothing in the system to notice.</para>
///
/// <para>ADR-0054 filed this as required change 9, "a larger defect than anything above", explicitly
/// not that decision's to fix.</para>
/// </summary>
public class RejectEmployeeReleasesSeatsTests
{
    private const string EmployeeId = "emp-rejected";
    private const string EmployeeUserId = "emp-rejected-user";
    private const string AdminEmail = "admin@cleansia.test";
    private const string AdminUserId = "admin-user";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<INotificationProducer> _producer = new();

    public RejectEmployeeReleasesSeatsTests()
    {
        var admin = User.CreateWithPassword(AdminEmail, "Passw0rd!", "Ad", "Min");
        admin.Id = AdminUserId;
        _users.Setup(r => r.GetByEmailAsync(AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);

        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmployee());
    }

    private RejectEmployee.Handler CreateHandler() =>
        new(_employees.Object, _users.Object, _orders.Object, _producer.Object,
            new TestUserSessionProvider(AdminUserId, AdminEmail), new AuditContext());

    private static Employee BuildEmployee()
    {
        var user = User.CreateWithPassword("rejected@cleansia.test", "Passw0rd!", "Re", "Jected");
        user.Id = EmployeeUserId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        return employee;
    }

    private static Order OrderHeldBy(string orderId, string employeeId, int maxEmployees = 1)
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "customer-user");
        order.Id = orderId;
        order.SetMaxEmployees(maxEmployees);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        var user = User.CreateWithPassword(employeeId + "@x.test", "Passw0rd!", "Emp", "Loyee");
        user.Id = employeeId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        return order;
    }

    private void ArrangeHeldOrders(params Order[] orders) =>
        _orders
            .Setup(r => r.GetFutureConfirmedOrdersForEmployeeAsync(
                EmployeeId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

    private async Task<bool> RejectAsync()
    {
        var result = await CreateHandler().Handle(
            new RejectEmployee.Command(EmployeeId, "documents look forged"), CancellationToken.None);
        return result.IsSuccess;
    }

    /// <summary>
    /// The whole point: the seat goes back to the pool. <c>UnassignEmployee</c> hard-deletes the row,
    /// which is the only thing that makes the order offerable again — a status flag on the assignment
    /// would still be counted by <c>AssignedEmployees.Count</c>.
    /// </summary>
    [Fact]
    public async Task Rejecting_A_Cleaner_Frees_Their_Future_Seats()
    {
        var order = OrderHeldBy("order-1", EmployeeId);
        ArrangeHeldOrders(order);

        Assert.True(await RejectAsync());

        Assert.DoesNotContain(order.AssignedEmployees, a => a.EmployeeId == EmployeeId);
        Assert.Empty(order.AssignedEmployees);
    }

    /// <summary>
    /// A two-seat job keeps its OTHER cleaner. The rejection is about one person; taking the whole crew
    /// off would turn one admin action into a second, larger outage.
    /// </summary>
    [Fact]
    public async Task Only_The_Rejected_Cleaners_Seat_Is_Taken()
    {
        var order = OrderHeldBy("order-2", EmployeeId, maxEmployees: 2);
        var colleagueUser = User.CreateWithPassword("keep@x.test", "Passw0rd!", "Keep", "Er");
        colleagueUser.Id = "emp-keep-user";
        var colleague = Employee.CreateWithUser(colleagueUser);
        colleague.Id = "emp-keep";
        order.AddAssignedEmployee(OrderEmployee.Create(order, colleague));
        ArrangeHeldOrders(order);

        Assert.True(await RejectAsync());

        Assert.Single(order.AssignedEmployees);
        Assert.Equal("emp-keep", order.AssignedEmployees.Single().EmployeeId);
    }

    /// <summary>
    /// Every released assignment tells the cleaner, once per assignment.
    ///
    /// <para><b>And the subjects must differ.</b> The outbox enforces <c>(QueueName, MessageKey)</c>
    /// UNIQUE and the violation lands inside the pipeline's commit, after the handler returned — so two
    /// releases sharing a key would not drop a push, they would roll the whole rejection back. The
    /// notifier keys on <c>AssignmentNotificationSubject.For(orderId, assignmentId)</c>; this asserts
    /// the distinctness that depends on rather than the format.</para>
    /// </summary>
    [Fact]
    public async Task Each_Released_Assignment_Notifies_The_Cleaner_Under_Its_Own_Key()
    {
        var subjects = new List<string?>();
        _producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, _, _, _, subject, _) => subjects.Add(subject))
            .Returns(Task.CompletedTask);

        ArrangeHeldOrders(
            OrderHeldBy("order-a", EmployeeId),
            OrderHeldBy("order-b", EmployeeId),
            OrderHeldBy("order-c", EmployeeId));

        Assert.True(await RejectAsync());

        Assert.Equal(3, subjects.Count);
        Assert.Equal(3, subjects.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The event is the revocation, not the rejection — the cleaner already knows they were rejected,
    /// and the order-side notice is what tells them the specific day is off their calendar.
    /// </summary>
    [Fact]
    public async Task The_Released_Assignment_Is_Announced_As_A_Revocation_To_The_Cleaner()
    {
        ArrangeHeldOrders(OrderHeldBy("order-1", EmployeeId));

        Assert.True(await RejectAsync());

        _producer.Verify(p => p.NotifyAsync(
                It.IsAny<string>(),
                NotificationEventCatalog.OrderAssignmentRevoked,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A cleaner holding nothing is the ordinary case and must not cost a notification or a write.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Holding_No_Future_Work_Is_Rejected_Silently()
    {
        ArrangeHeldOrders();

        Assert.True(await RejectAsync());

        _producer.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Only <c>Confirmed</c> work in the future is asked for. In-progress work is a cleaner standing in
    /// somebody's home, and pulling that seat mid-clean is worse than the rejection waiting for an
    /// admin — so the boundary lives in the query and this pins that the handler asks for exactly it.
    /// </summary>
    [Fact]
    public async Task Only_Future_Confirmed_Work_Is_Asked_For()
    {
        ArrangeHeldOrders();

        Assert.True(await RejectAsync());

        _orders.Verify(r => r.GetFutureConfirmedOrdersForEmployeeAsync(
                EmployeeId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orders.VerifyNoOtherCalls();
    }
}
