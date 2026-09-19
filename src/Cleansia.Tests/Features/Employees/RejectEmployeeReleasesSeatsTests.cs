using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Orders;
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
///
/// <para><b>And a released seat that empties the crew walks the order back.</b> <c>Confirmed</c> says a
/// cleaner took the job; a rejection that leaves nobody on a Confirmed order returns it to <c>New</c>
/// through the one domain writer, ends a live reservation held by the rejected cleaner (an order back
/// on the board must be ON the board), re-advertises a seat a remaining crew keeps, and tells the
/// company's administrators once per emptied order — including the administrator who clicked.</para>
/// </summary>
public class RejectEmployeeReleasesSeatsTests
{
    private const string EmployeeId = "emp-rejected";
    private const string EmployeeUserId = "emp-rejected-user";
    private const string AdminEmail = "admin@cleansia.test";
    private const string AdminUserId = "admin-user";
    private const string CompanyId = "company-reject";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];

    public RejectEmployeeReleasesSeatsTests()
    {
        var admin = User.CreateWithPassword(AdminEmail, "Passw0rd!", "Ad", "Min");
        admin.Id = AdminUserId;
        _users.Setup(r => r.GetByEmailAsync(AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);

        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmployee());

        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private RejectEmployee.Handler CreateHandler() =>
        new(_employees.Object, _users.Object, _orders.Object, _producer.Object,
            new TestUserSessionProvider(AdminUserId, AdminEmail), new AuditContext(), _adminNotifier.Object);

    private static Employee BuildEmployee()
    {
        var user = User.CreateWithPassword("rejected@cleansia.test", "Passw0rd!", "Re", "Jected");
        user.Id = EmployeeUserId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        return employee;
    }

    /// <param name="heldFor">
    /// A live preferred reservation's beneficiary, granted BEFORE the seat is taken — the order the
    /// aggregate enforces, and the real one: the preferred cleaner is offered the job, then takes it.
    /// </param>
    private static Order OrderHeldBy(
        string orderId, string employeeId, int maxEmployees = 1, string? heldFor = null)
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "customer-user");
        order.Id = orderId;
        order.TenantId = CompanyId;
        order.SetMaxEmployees(maxEmployees);
        var created = DateTimeOffset.UtcNow.AddDays(-1);
        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", created);
        order.AddOrderStatus(initial);
        var confirmed = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
        confirmed.Created("test", created.AddMinutes(1));
        order.AddOrderStatus(confirmed);

        if (heldFor is not null)
        {
            var now = DateTime.UtcNow;
            order.GrantPreferredHold(heldFor, now.AddHours(12), now, maxRounds: 3);
        }

        var user = User.CreateWithPassword(employeeId + "@x.test", "Passw0rd!", "Emp", "Loyee");
        user.Id = employeeId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        return order;
    }

    private static Order WithColleague(Order order, string colleagueId)
    {
        var colleagueUser = User.CreateWithPassword(colleagueId + "@x.test", "Passw0rd!", "Keep", "Er");
        colleagueUser.Id = colleagueId + "-user";
        var colleague = Employee.CreateWithUser(colleagueUser);
        colleague.Id = colleagueId;
        order.AddAssignedEmployee(OrderEmployee.Create(order, colleague));
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
        var order = WithColleague(OrderHeldBy("order-2", EmployeeId, maxEmployees: 2), "emp-keep");
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
        _adminNotifier.VerifyNoOtherCalls();
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
    // ── The crew empties ──

    /// <summary>
    /// The rejected cleaner was the only one on a Confirmed order: nobody has it any more, so it goes
    /// back to New with the next history row, and the company is told once with the cause.
    /// </summary>
    [Fact]
    public async Task Releasing_The_Last_Seat_Returns_The_Order_To_New_And_Tells_The_Administrators()
    {
        var order = OrderHeldBy("order-1", EmployeeId);
        var releasedAssignmentId = order.AssignedEmployees.Single().Id;
        var lastSequence = order.OrderStatusHistory.Max(s => s.Sequence);
        ArrangeHeldOrders(order);

        Assert.True(await RejectAsync());

        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        var latest = order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First();
        Assert.Equal(OrderStatus.New, latest.Status);
        Assert.Equal(lastSequence + 1, latest.Sequence);

        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderCrewLost, raised.Key);
        Assert.Equal(CompanyId, raised.TenantId);
        Assert.Equal(AssignmentNotificationSubject.For("order-1", releasedAssignmentId), raised.Subject);
        Assert.Equal("rejected", raised.Args["cause"]);
        Assert.Equal(nameof(OrderStatus.Confirmed), raised.Args["statusAtLoss"]);
        Assert.Equal("order-1", raised.Args["orderId"]);
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal(5, raised.Args.Count);
    }

    [Fact]
    public async Task Every_Emptied_Order_Raises_Its_Own_Admin_Event()
    {
        ArrangeHeldOrders(
            OrderHeldBy("order-a", EmployeeId),
            OrderHeldBy("order-b", EmployeeId));

        Assert.True(await RejectAsync());

        Assert.Equal(2, _raised.Count);
        Assert.Equal(2, _raised.Select(e => e.Subject).Distinct(StringComparer.Ordinal).Count());
        Assert.All(_raised, e => Assert.Equal("rejected", e.Args["cause"]));
    }

    /// <summary>
    /// A crew remains: the job is still staffed, so the status holds — but the freed seat must reach
    /// the board, and the digest only sees an order with a fresh history row. Same-value, exactly as a
    /// drop re-advertises, so the two release writers cannot disagree.
    /// </summary>
    [Fact]
    public async Task A_Crew_That_Remains_Keeps_Confirmed_Re_Advertises_And_Tells_Nobody()
    {
        var order = WithColleague(OrderHeldBy("order-2", EmployeeId, maxEmployees: 2), "emp-keep");
        var before = order.OrderStatusHistory.Count;
        ArrangeHeldOrders(order);

        Assert.True(await RejectAsync());

        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(before + 1, order.OrderStatusHistory.Count);
        Assert.Equal(OrderStatus.Confirmed, order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First().Status);
        Assert.Empty(_raised);
    }

    /// <summary>
    /// A live reservation hides the order from everyone but its beneficiary for up to twelve hours.
    /// Leaving one standing for a cleaner who can no longer work would take the seat this rejection
    /// just freed off the board on behalf of nobody.
    /// </summary>
    [Fact]
    public async Task A_Live_Hold_For_The_Rejected_Cleaner_Is_Ended()
    {
        var order = OrderHeldBy("order-held", EmployeeId, heldFor: EmployeeId);
        Assert.True(order.PreferredHoldUntilUtc > DateTime.UtcNow);
        ArrangeHeldOrders(order);

        Assert.True(await RejectAsync());

        Assert.True(order.PreferredHoldUntilUtc <= DateTime.UtcNow);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
    }

    /// <summary>Another cleaner's reservation is not this rejection's to cancel.</summary>
    [Fact]
    public async Task A_Hold_For_Another_Cleaner_Is_Left_Standing()
    {
        var order = OrderHeldBy("order-other-hold", EmployeeId, maxEmployees: 2, heldFor: "emp-other");
        var holdUntil = order.PreferredHoldUntilUtc;
        ArrangeHeldOrders(order);

        Assert.True(await RejectAsync());

        Assert.Equal("emp-other", order.PreferredEmployeeId);
        Assert.Equal(holdUntil, order.PreferredHoldUntilUtc);
    }
}
