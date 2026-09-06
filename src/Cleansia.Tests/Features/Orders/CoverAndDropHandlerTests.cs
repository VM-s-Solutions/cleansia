using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
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
/// </summary>
public class CoverAndDropHandlerTests
{
    private const string OrderId = "order-drop-1";
    private const string EmployeeId = "emp-drop-1";
    private const string OtherEmployeeId = "emp-drop-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IEmployeeActionAuditRepository> _audit = new();

    private static Order OrderWith(params string[] assignedEmployeeIds)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.Confirmed, maxEmployees: 2);

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
            _notifications.Object, _audit.Object);

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
}
