using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <c>RequestCover</c> and <c>DropOrder</c> — the two ways a cleaner comes off a job, and the first
/// things in the platform that ever release a seat.
///
/// <para><b>The re-advertisement is the load-bearing assertion here.</b> The digest decides an order is
/// new to a cleaner with <c>OrderStatusHistory.Any(s =&gt; s.CreatedOn &gt; since)</c>. A seat released
/// without a new status row is therefore invisible to every cleaner forever — the release succeeds, the
/// board never hears about it, and the failure is completely silent. Nothing else in the platform reads
/// the same-value row, so nothing else would ever catch its absence.</para>
///
/// <para><b>Neither command moves money</b> (owner ruling 2026-09-06). A drop does not cancel the
/// booking, so there is nothing to refund at that moment; an order that reaches its slot with nobody on
/// it is the sweep's business. Two money paths for one failure would have needed a guard between them
/// against paying twice.</para>
///
/// <para><b>A drop that empties the crew is two facts, not one.</b> <c>Confirmed</c> says a cleaner
/// took the job, so a <c>Confirmed</c> order with nobody left on it goes back to <c>New</c>; and the
/// company's administrators are told whenever the crew empties, at ANY status — an order dropped
/// <c>OnTheWay</c> is not walked back (a cleaner may be in the home) and no sweep catches it, so the
/// alarm is the only thing that does.</para>
/// </summary>
public class CoverAndDropHandlerTests
{
    private const string OrderId = "order-drop-1";
    private const string EmployeeId = "emp-drop-1";
    private const string OtherEmployeeId = "emp-drop-2";
    private const string CompanyId = "company-drop";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IEmployeeActionAuditRepository> _audit = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];

    public CoverAndDropHandlerTests()
    {
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private static Order OrderWith(params string[] assignedEmployeeIds) =>
        OrderAt(OrderStatus.Confirmed, assignedEmployeeIds);

    private static Order OrderAt(OrderStatus status, params string[] assignedEmployeeIds)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, status, maxEmployees: 2);
        order.TenantId = CompanyId;

        foreach (var id in assignedEmployeeIds)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(id, ContractStatus.Approved)));
        }

        return order;
    }

    private void Arrange(Order order, string? callerEmployeeId = EmployeeId)
    {
        _orderRepository.Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerEmployeeId);
        // The seat-open fan-out reads this. Empty is the honest default here: who gets woken is
        // SeatOpenedNotifier's own concern and has its own tests, and these are about the release.
        _employees.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(Array.Empty<Employee>().AsQueryable().BuildMock());
    }

    private RequestCover.Handler CoverHandler() =>
        new(_orderRepository.Object, _accessService.Object, _employees.Object,
            _notifications.Object, _audit.Object);

    private DropOrder.Handler DropHandler() =>
        new(_orderRepository.Object, _accessService.Object, _employees.Object,
            _notifications.Object, _audit.Object, _adminNotifier.Object);

    // ── RequestCover ──

    [Fact]
    public async Task RequestingCoverKeepsTheCleanerAssigned()
    {
        var order = OrderWith(EmployeeId, OtherEmployeeId);
        Arrange(order);

        var result = await CoverHandler().Handle(new RequestCover.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        // Still on the job — the whole difference between this and a drop.
        Assert.Contains(order.AssignedEmployees, oe => oe.EmployeeId == EmployeeId);
        Assert.Equal(2, order.AssignedEmployees.Count);
        Assert.Equal(1, order.CoverSeatsOpen);
        // ...and the seat is now takeable although the crew is full.
        Assert.False(order.HasAvailableSpots);
        Assert.True(order.HasTakeableSeat);
    }

    /// <summary>Without this row the released seat reaches no cleaner, ever. → NewJobsDigestService</summary>
    [Fact]
    public async Task RequestingCoverReAdvertisesTheOrder()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);
        var before = order.OrderStatusHistory.Count;
        var statusBefore = order.CurrentStatus;

        await CoverHandler().Handle(new RequestCover.Command(OrderId), default);

        Assert.Equal(before + 1, order.OrderStatusHistory.Count);
        // Same value: the row exists to be NEW, not to move the order.
        Assert.Equal(statusBefore, order.CurrentStatus);
    }

    [Fact]
    public async Task RequestingCoverTwiceIsASuccessAndWritesOneRecord()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);

        var first = await CoverHandler().Handle(new RequestCover.Command(OrderId), default);
        var second = await CoverHandler().Handle(new RequestCover.Command(OrderId), default);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.CoverRequestedAt, second.Value.CoverRequestedAt);
        _audit.Verify(
            r => r.Add(It.Is<EmployeeActionAudit>(a => a.Action == EmployeeAuditAction.CoverRequested)),
            Times.Once);
    }

    [Fact]
    public async Task ACleanerNotOnTheJobCannotRequestCover()
    {
        var order = OrderWith(OtherEmployeeId);
        Arrange(order);

        var result = await CoverHandler().Handle(new RequestCover.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, order.CoverSeatsOpen);
    }

    // ── DropOrder ──

    /// <summary>
    /// THE RULING. A drop releases the seat and leaves the booking alive — the job runs with whoever is
    /// left. The order must NOT move to Cancelled, and no refund is staged.
    /// </summary>
    [Fact]
    public async Task DroppingReleasesTheSeatAndLeavesTheBookingAlive()
    {
        var order = OrderWith(EmployeeId, OtherEmployeeId);
        Arrange(order);

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(order.AssignedEmployees, oe => oe.EmployeeId == EmployeeId);
        Assert.Equal(1, result.Value.CrewRemaining);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Null(order.CancellationRefundAmount);
    }

    /// <summary>
    /// The last cleaner walking is still not a cancellation. The order is now heading for the sweep
    /// unless somebody takes it, and the response says so rather than letting the cleaner believe the
    /// booking is covered.
    /// </summary>
    [Fact]
    public async Task DroppingTheLastSeatStillDoesNotCancel()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.CrewRemaining);
        Assert.Empty(order.AssignedEmployees);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
    }

    [Fact]
    public async Task DroppingReAdvertisesTheOrder()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);
        var before = order.OrderStatusHistory.Count;

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.Equal(before + 1, order.OrderStatusHistory.Count);
    }

    [Fact]
    public async Task DroppingRecordsWhoLeftAndWhen()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        _audit.Verify(
            r => r.Add(It.Is<EmployeeActionAudit>(a =>
                a.EmployeeId == EmployeeId
                && a.OrderId == OrderId
                && a.Action == EmployeeAuditAction.OrderDropped)),
            Times.Once);
    }

    /// <summary>
    /// A cleaner who is already off the job gets a success, not an error — the state they wanted holds.
    /// Same idempotent silence DeclinePreferredOffer gives a second decline.
    /// </summary>
    [Fact]
    public async Task DroppingAJobYouAreNotOnIsANoOpSuccess()
    {
        var order = OrderWith(OtherEmployeeId);
        Arrange(order);

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Single(order.AssignedEmployees);
        _audit.Verify(r => r.Add(It.IsAny<EmployeeActionAudit>()), Times.Never);
    }
    // ── DropOrder: the crew empties ──

    /// <summary>
    /// The last cleaner leaving a Confirmed order makes the word false — nobody took it any more — so
    /// the order returns to New through the one domain writer, with a fresh history row (which is also
    /// the re-advertisement the digest needs), and the company is told once.
    /// </summary>
    [Fact]
    public async Task DroppingTheLastSeatOfAConfirmedOrderReturnsItToNewAndTellsTheAdministrators()
    {
        var order = OrderWith(EmployeeId);
        var releasedAssignmentId = order.AssignedEmployees.Single().Id;
        Arrange(order);
        var lastSequence = order.OrderStatusHistory.Max(s => s.Sequence);

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        var latest = order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First();
        Assert.Equal(OrderStatus.New, latest.Status);
        Assert.Equal(lastSequence + 1, latest.Sequence);

        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderCrewLost, raised.Key);
        Assert.Equal(CompanyId, raised.TenantId);
        Assert.Equal(AssignmentNotificationSubject.For(OrderId, releasedAssignmentId), raised.Subject);
        Assert.Equal("dropped", raised.Args["cause"]);
        Assert.Equal(nameof(OrderStatus.Confirmed), raised.Args["statusAtLoss"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal(
            DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc).ToString("O"),
            raised.Args["cleaningDateTime"]);
        Assert.Equal(5, raised.Args.Count);
        _audit.Verify(
            r => r.Add(It.Is<EmployeeActionAudit>(a => a.Action == EmployeeAuditAction.OrderDropped)),
            Times.Once);
    }

    /// <summary>A crew remains: the job is still staffed, so the status holds and nobody is alarmed.</summary>
    [Fact]
    public async Task DroppingOneSeatOfTwoKeepsConfirmedReAdvertisesAndTellsNobody()
    {
        var order = OrderWith(EmployeeId, OtherEmployeeId);
        Arrange(order);
        var before = order.OrderStatusHistory.Count;

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(before + 1, order.OrderStatusHistory.Count);
        Assert.Equal(OrderStatus.Confirmed, order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First().Status);
        Assert.Empty(_raised);
    }

    /// <summary>
    /// Past Confirmed the platform never walks the status back — a cleaner may be standing in the home —
    /// and no sweep selects an unstaffed OnTheWay order. The alarm is the only thing that fires, and it
    /// says which status the crew was lost at so the sentence can say the clean was under way.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    public async Task DroppingTheLastSeatOfAnOrderUnderWayKeepsTheStatusAndTellsTheAdministrators(OrderStatus status)
    {
        var order = OrderAt(status, EmployeeId);
        Arrange(order);
        var before = order.OrderStatusHistory.Count;

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, order.CurrentStatus);
        Assert.Equal(before + 1, order.OrderStatusHistory.Count);
        Assert.Equal(status, order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First().Status);
        var raised = Assert.Single(_raised);
        Assert.Equal("dropped", raised.Args["cause"]);
        Assert.Equal(status.ToString(), raised.Args["statusAtLoss"]);
    }

    /// <summary>
    /// The customer's keyset is untouched by a walk-back: a drop moves no money and cancels nothing, and
    /// the slot-time sweep is the one place they learn nobody came. The only push here is the seat-open
    /// wake to other cleaners, and this fixture's cohort is empty.
    /// </summary>
    [Fact]
    public async Task DroppingTheLastSeatSendsTheCustomerNothing()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(),
                It.Is<string>(key => NotificationFeedEventKeys.Customer.Contains(key)),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
