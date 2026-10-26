using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
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
/// took the job, so a <c>Confirmed</c> order with nobody left on it goes back to <c>New</c>. A job under
/// way cannot be dropped at all (owner ruling 2026-10-25), so the crew empties only at <c>New</c> or
/// <c>Confirmed</c>. Every drop leaves the job short of the cleaners it needs, so every drop tells the
/// customer that a cleaner can no longer come, and the administrators that the crew is short or
/// gone.</para>
/// </summary>
public class CoverAndDropHandlerTests
{
    private const string OrderId = "order-drop-1";
    private const string EmployeeId = "emp-drop-1";
    private const string OtherEmployeeId = "emp-drop-2";
    private const string CompanyId = "company-drop";
    private const string CustomerUserId = "user-drop-customer";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IEmployeeActionAuditRepository> _audit = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];
    private readonly List<(string Queue, string Key, object Message)> _staged = [];

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

    /// <summary>A Confirmed order booked from an account, so the customer has somebody to push to.</summary>
    private static Order AccountOrderWith(params string[] assignedEmployeeIds)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Confirmed, assignedEmployeeIds[0], maxEmployees: 2, userId: CustomerUserId);
        order.TenantId = CompanyId;

        foreach (var id in assignedEmployeeIds.Skip(1))
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(id, ContractStatus.Approved)));
        }

        return order;
    }

    /// <summary>
    /// A Confirmed account order reserved for <paramref name="beneficiaryId"/> until
    /// <paramref name="holdUntilUtc"/>, granted before <paramref name="cleanerId"/> took the seat — the order
    /// the aggregate enforces.
    /// </summary>
    private static Order ReservedAccountOrder(string beneficiaryId, DateTime holdUntilUtc, string cleanerId)
    {
        var order = Order.Create(
            customerName: "Reserved Customer",
            customerEmail: "reserved@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            preferredEmployeeId: beneficiaryId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.TenantId = CompanyId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.GrantPreferredHold(
            beneficiaryId, holdUntilUtc, holdUntilUtc.AddHours(-12), BookingPolicy.MaxPreferredOfferRounds);
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee(cleanerId, ContractStatus.Approved)));
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
            _notifications.Object, _audit.Object, _adminNotifier.Object, new RecordingDispatch(_staged));

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

    /// <summary>
    /// The event's subject is the released assignment, and a second drop of a job the cleaner is no
    /// longer on is the no-op success above: the administrators are told once, so the e-mail keyed on
    /// that subject is enqueued once and a redelivered command never collides on the outbox.
    /// </summary>
    [Fact]
    public async Task DroppingTheSameSeatTwiceTellsTheAdministratorsOnce()
    {
        var order = OrderWith(EmployeeId);
        Arrange(order);
        var handler = DropHandler();

        var first = await handler.Handle(new DropOrder.Command(OrderId), default);
        var second = await handler.Handle(new DropOrder.Command(OrderId), default);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(_raised);
    }

    /// <summary>
    /// A crew remains, so the status holds and the freed seat is re-advertised; the job is now short of a
    /// cleaner, so the administrators are told the crew is short — the same args as the empty crew's event,
    /// under the released assignment.
    /// </summary>
    [Fact]
    public async Task DroppingOneSeatOfTwoKeepsConfirmedReAdvertisesAndTellsTheAdministratorsTheCrewIsShort()
    {
        var order = OrderWith(EmployeeId, OtherEmployeeId);
        var releasedAssignmentId = order.AssignedEmployees.Single(a => a.EmployeeId == EmployeeId).Id;
        Arrange(order);
        var before = order.OrderStatusHistory.Count;

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(before + 1, order.OrderStatusHistory.Count);
        Assert.Equal(OrderStatus.Confirmed, order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First().Status);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderCrewShort, raised.Key);
        Assert.Equal(CompanyId, raised.TenantId);
        Assert.Equal(AssignmentNotificationSubject.For(OrderId, releasedAssignmentId), raised.Subject);
        Assert.Equal("dropped", raised.Args["cause"]);
        Assert.Equal(nameof(OrderStatus.Confirmed), raised.Args["statusAtLoss"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
        Assert.Equal(5, raised.Args.Count);
    }

    /// <summary>
    /// A drop that leaves a crew changes no order column, so without the stamp the order's row is not in
    /// the commit and its CurrentStatus concurrency token is never compared against a status another
    /// writer moved meanwhile. → DropOrderLockoutRaceTests
    /// </summary>
    [Fact]
    public async Task DroppingOneSeatOfTwoStampsTheOrderSoItsStatusIsCheckedAtCommit()
    {
        var order = OrderWith(EmployeeId, OtherEmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.NotNull(order.UpdatedOn);
    }

    /// <summary>
    /// Once a cleaner has set off or started, the job keeps its crew (owner ruling 2026-10-25). The drop
    /// is refused with the key the status check has always answered, and nothing moves: the seat stays,
    /// no status row and no drop record are written, and nobody is told anything.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    public async Task DroppingAJobUnderWayIsRefusedAndChangesNothing(OrderStatus status)
    {
        var order = OrderAt(status, EmployeeId);
        Arrange(order);
        var before = order.OrderStatusHistory.Count;

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotConfirmed, result.Error!.Message);
        Assert.Equal(EmployeeId, Assert.Single(order.AssignedEmployees).EmployeeId);
        Assert.Equal(status, order.CurrentStatus);
        Assert.Equal(before, order.OrderStatusHistory.Count);
        Assert.Null(order.UpdatedOn);
        _audit.Verify(r => r.Add(It.IsAny<EmployeeActionAudit>()), Times.Never);
        _notifications.VerifyNoOtherCalls();
        Assert.Empty(_raised);
        Assert.Empty(_staged);
    }

    /// <summary>
    /// Before anyone sets off the drop is the cleaner's to make. <c>New</c> stays on the gate with
    /// <c>Confirmed</c> although no writer leaves a cleaner on a <c>New</c> order: it admits nothing more,
    /// and it is the gate the three partner apps apply before they send.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task DroppingBeforeTheJobIsUnderWayReleasesTheSeat(OrderStatus status)
    {
        var order = OrderAt(status, EmployeeId, OtherEmployeeId);
        Arrange(order);

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(OtherEmployeeId, Assert.Single(order.AssignedEmployees).EmployeeId);
        Assert.Equal(1, result.Value.CrewRemaining);
    }

    // ── DropOrder: the customer is told ──

    /// <summary>
    /// The last cleaner leaving tells the customer that a cleaner can no longer come and the booking is
    /// offered to cleaners again: one push and feed row about the assignment that ended, and one e-mail
    /// staged in the same unit of work under a key of its own for this release.
    /// </summary>
    [Fact]
    public async Task DroppingTheLastSeatOfAConfirmedOrderTellsTheCustomerTheBookingIsOfferedAgain()
    {
        var order = AccountOrderWith(EmployeeId);
        var releasedAssignmentId = order.AssignedEmployees.Single().Id;
        Arrange(order);

        var result = await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        _notifications.Verify(n => n.NotifyAsync(
                CustomerUserId,
                NotificationEventCatalog.OrderCleanerReleased,
                It.Is<Dictionary<string, string>>(args => args.Count == 2
                    && args["orderId"] == OrderId
                    && args["orderNumber"] == order.DisplayOrderNumber),
                CompanyId,
                AssignmentNotificationSubject.For(OrderId, releasedAssignmentId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var (queue, key, message) = Assert.Single(_staged);
        Assert.Equal(QueueNames.SendEmail, queue);
        Assert.Equal(MessageKeys.OrderCleanerReleasedEmail(OrderId, releasedAssignmentId), key);
        var envelope = Assert.IsType<QueueEnvelope<SendOrderCleanerReleasedEmailMessage>>(message);
        Assert.Equal(key, envelope.MessageKey);
        Assert.Equal(CompanyId, envelope.TenantId);
        Assert.Equal(
            new SendOrderCleanerReleasedEmailMessage(OrderId, releasedAssignmentId, "en", CompanyId),
            envelope.Payload);
    }

    /// <summary>
    /// One of two cleaners leaving still leaves the job short, so the customer is told the same thing as
    /// when the last one leaves — the copy counts no cleaners — under the assignment that ended.
    /// </summary>
    [Fact]
    public async Task DroppingOneSeatOfTwoTellsTheCustomerACleanerCanNoLongerCome()
    {
        var order = AccountOrderWith(EmployeeId, OtherEmployeeId);
        var releasedAssignmentId = order.AssignedEmployees.Single(a => a.EmployeeId == EmployeeId).Id;
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        _notifications.Verify(n => n.NotifyAsync(
                CustomerUserId,
                NotificationEventCatalog.OrderCleanerReleased,
                It.Is<Dictionary<string, string>>(args => args.Count == 2 && args["orderId"] == OrderId),
                CompanyId,
                AssignmentNotificationSubject.For(OrderId, releasedAssignmentId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        var (queue, key, _) = Assert.Single(_staged);
        Assert.Equal(QueueNames.SendEmail, queue);
        Assert.Equal(MessageKeys.OrderCleanerReleasedEmail(OrderId, releasedAssignmentId), key);
    }

    /// <summary>
    /// A guest has no account to push to, so the e-mail to the booking's own address is the whole notice.
    /// The only push here is the seat-open wake to other cleaners, and this fixture's cohort is empty.
    /// </summary>
    [Fact]
    public async Task DroppingTheLastSeatOfAGuestOrderEmailsTheGuestAndPushesNobody()
    {
        var order = OrderWith(EmployeeId);
        var releasedAssignmentId = order.AssignedEmployees.Single().Id;
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        VerifyNoCustomerPush();
        var (queue, key, _) = Assert.Single(_staged);
        Assert.Equal(QueueNames.SendEmail, queue);
        Assert.Equal(MessageKeys.OrderCleanerReleasedEmail(OrderId, releasedAssignmentId), key);
    }

    // ── DropOrder: the reservation the release ends ──

    /// <summary>
    /// The cleaner the customer asked for takes the reserved job and drops it while the reservation runs.
    /// The drop ends the reservation, and the release notice is its one announcement: the receipt is stamped
    /// at the drop's instant, so the five-minute lapse sweep sends no "open to all cleaners" push after it.
    /// </summary>
    [Fact]
    public async Task The_Chosen_Cleaner_Dropping_A_Reserved_Job_Marks_The_Reservation_Announced()
    {
        var order = ReservedAccountOrder(EmployeeId, DateTime.UtcNow.AddHours(6), EmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.NotNull(order.PreferredOfferLapseNotifiedAt);
        Assert.Equal(order.PreferredHoldUntilUtc, order.PreferredOfferLapseNotifiedAt);
        _notifications.Verify(n => n.NotifyAsync(
                CustomerUserId, NotificationEventCatalog.OrderCleanerReleased, It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), NotificationEventCatalog.PreferredOfferClosed, It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>A reservation that had already lapsed when the cleaner dropped is announced by the release too.</summary>
    [Fact]
    public async Task A_Drop_After_The_Reservation_Lapsed_Marks_It_Announced_At_The_Drop()
    {
        var before = DateTime.UtcNow;
        var order = ReservedAccountOrder(EmployeeId, before.AddHours(-1), EmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.InRange(order.PreferredOfferLapseNotifiedAt!.Value, before, DateTime.UtcNow);
    }

    /// <summary>
    /// Another cleaner still holds the reservation when the crew empties — reachable only through an
    /// administrator's placement during the hold. The release does not announce what it did not end: the
    /// lapse sweep announces it when it lapses.
    /// </summary>
    [Fact]
    public async Task A_Drop_While_Another_Cleaners_Reservation_Runs_Leaves_It_Unannounced()
    {
        var order = ReservedAccountOrder(OtherEmployeeId, DateTime.UtcNow.AddHours(6), EmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Null(order.PreferredOfferLapseNotifiedAt);
    }

    [Fact]
    public async Task A_Drop_Of_An_Order_With_No_Reservation_Stamps_No_Receipt()
    {
        var order = AccountOrderWith(EmployeeId);
        Arrange(order);

        await DropHandler().Handle(new DropOrder.Command(OrderId), default);

        Assert.Null(order.PreferredOfferLapseNotifiedAt);
    }

    private void VerifyNoCustomerPush() =>
        _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(),
                It.Is<string>(key => NotificationFeedEventKeys.Customer.Contains(key)),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private sealed class RecordingDispatch(List<(string Queue, string Key, object Message)> staged) : IPendingDispatch
    {
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            staged.Add((queueName, messageKey, message!));

        public IReadOnlyList<PendingMessage> Drain() => [];
    }
}
