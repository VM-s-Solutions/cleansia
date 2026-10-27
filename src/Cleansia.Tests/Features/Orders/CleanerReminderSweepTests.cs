using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The two per-job cleaner reminders.
///
/// <para>The case that matters most is the two-seat one: an order's crew is
/// <c>ceil(EstimatedTime / 120)</c>, so every booking over 120 minutes carries two cleaners. A reminder
/// stamp on the ORDER would remind the first and silently skip the second, which is why the stamps live
/// on the assignment row.</para>
/// </summary>
public class CleanerReminderSweepTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly Mock<IUserNotificationRepository> _userNotifications = new();

    private SendCleanerJobReminders.Handler Handler() => new(
        _orderRepository.Object,
        _producer.Object,
        _adminNotifier.Object,
        _userNotifications.Object,
        _tenantProvider.Object,
        _unitOfWork.Object,
        NullLogger<SendCleanerJobReminders.Handler>.Instance, TimeProvider.System);

    private Order ArrangeOrder(int minutesOut, int crew, OrderStatus status = OrderStatus.Confirmed)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            "order-1", status, maxEmployees: crew,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(minutesOut));

        for (var i = 1; i <= crew; i++)
        {
            var employee = ValidatorTestHelpers.BuildEmployee($"emp-{i}", ContractStatus.Approved);
            order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        }

        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        OnAJobRightNow();
        return order;
    }

    /// <summary>Nobody is out working unless a case says so.</summary>
    private void OnAJobRightNow(params string[] employeeIds) =>
        _orderRepository
            .Setup(r => r.GetEmployeeIdsCurrentlyOnAJobAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(employeeIds, StringComparer.Ordinal));

    private List<string> SentEventKeys()
    {
        var keys = new List<string>();
        _producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, key, _, _, _, _) => keys.Add(key))
            .Returns(Task.CompletedTask);
        return keys;
    }

    /// <summary>
    /// The reason the stamps are on the assignment row. A scalar on the order would send one reminder
    /// and leave the second cleaner to find out on their own.
    /// </summary>
    [Fact]
    public async Task Every_Cleaner_On_A_Two_Seat_Job_Is_Reminded()
    {
        var keys = SentEventKeys();
        var order = ArrangeOrder(minutesOut: 120, crew: 2);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.SoonSent);
        Assert.Equal(2, keys.Count(k => k == NotificationEventCatalog.ReminderSoon));
        Assert.All(order.AssignedEmployees, a => Assert.NotNull(a.ReminderSoonSentAt));
    }

    [Fact]
    public async Task A_Second_Tick_Does_Not_Remind_Twice()
    {
        SentEventKeys();
        ArrangeOrder(minutesOut: 120, crew: 1);

        await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);
        var second = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(0, second.Value!.SoonSent);
    }

    [Fact]
    public async Task Close_To_The_Start_A_Cleaner_Who_Has_Not_Set_Off_Is_Nudged()
    {
        var keys = SentEventKeys();
        ArrangeOrder(minutesOut: 30, crew: 1);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.NudgesSent);
        Assert.Contains(NotificationEventCatalog.ReminderNotStarted, keys);
    }

    /// <summary>
    /// The nudge's whole justification. A cleaner already travelling must never be asked whether they
    /// have set off — that is the difference between a useful reminder and noise.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task A_Cleaner_Already_Under_Way_Is_Never_Nudged(OrderStatus status)
    {
        SentEventKeys();
        ArrangeOrder(minutesOut: 30, crew: 1, status: status);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.NudgesSent);
        Assert.Equal(0, result.Value.SoonSent);
    }

    [Fact]
    public async Task A_Job_Outside_Both_Windows_Is_Left_Alone()
    {
        SentEventKeys();
        ArrangeOrder(minutesOut: 60, crew: 1);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.SoonSent);
        Assert.Equal(0, result.Value.NudgesSent);
    }

    /// <summary>
    /// The nudge asks a cleaner to SET OFF. A cleaner already <c>OnTheWay</c> or <c>InProgress</c> on
    /// another job cannot, and telling them to is the reminder reading as noise.
    ///
    /// <para>This is not an edge case: back-to-back jobs are the normal shape of a full day, and the
    /// second job's nudge window falls inside the first job. The two-hour notice is deliberately NOT
    /// suppressed — knowing what is next while finishing this one is useful.</para>
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Already_Out_On_Another_Job_Is_Not_Nudged()
    {
        SentEventKeys();
        ArrangeOrder(minutesOut: 30, crew: 1);
        OnAJobRightNow("emp-1");

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.NudgesSent);
    }

    [Fact]
    public async Task A_Cleaner_Out_On_Another_Job_Still_Gets_The_Two_Hour_Notice()
    {
        SentEventKeys();
        ArrangeOrder(minutesOut: 120, crew: 1);
        OnAJobRightNow("emp-1");

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.SoonSent);
    }

    /// <summary>
    /// Rejecting a cleaner does not take them off their live orders, so without a recipient predicate
    /// the platform tells somebody it has just barred from working that their job starts in two hours —
    /// for work <c>StartOrder</c> would then refuse to let them start.
    /// </summary>
    [Theory]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Terminated)]
    [InlineData(ContractStatus.Pending)]
    public async Task A_Cleaner_Who_May_No_Longer_Work_Is_Not_Reminded(ContractStatus status)
    {
        SentEventKeys();
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            "order-1", OrderStatus.Confirmed, maxEmployees: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(120));
        order.AddAssignedEmployee(
            OrderEmployee.Create(order, ValidatorTestHelpers.BuildEmployee("emp-1", status)));
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        OnAJobRightNow();

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.SoonSent);
        Assert.Equal(0, result.Value.NudgesSent);
    }

    /// <summary>
    /// The windows must not overlap and the nudge must sit inside the notice — the validator states it,
    /// and this pins that the shipped defaults actually satisfy it.
    /// </summary>
    [Fact]
    public void The_Default_Windows_Are_Ordered_And_Disjoint()
    {
        var command = new SendCleanerJobReminders.Command();
        var result = new SendCleanerJobReminders.Validator().Validate(command);

        Assert.True(result.IsValid);
        Assert.True(command.NudgeLeadMinutesHigh < command.SoonLeadMinutesLow);
    }

    private Order ArrangeStartedAgo(int minutesAgo, OrderStatus status = OrderStatus.Confirmed, int seats = 1)
    {
        var order = ArrangeOrder(minutesOut: -minutesAgo, crew: 1, status);
        order.SetMaxEmployees(seats);
        order.TenantId = "tenant-a";
        return order;
    }

    private void Alerted(Order order, Times times) =>
        _adminNotifier.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Key == AdminNotificationEventCatalog.OrderCleanerNotStarted
                && e.TenantId == "tenant-a"
                && e.Subject == order.Id
                && e.Args["orderId"] == order.Id),
            It.IsAny<CancellationToken>()), times);

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.OnTheWay)]
    public async Task A_Job_Nobody_Has_Started_Half_An_Hour_After_Its_Start_Alerts_The_Administrators(OrderStatus status)
    {
        var order = ArrangeStartedAgo(minutesAgo: 35, status);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Alerted(order, Times.Once());
        Assert.Equal(1, result.Value!.NotStartedAlerts);
    }

    [Fact]
    public async Task A_Partly_Filled_Crew_Nobody_Has_Started_Alerts_Too()
    {
        var order = ArrangeStartedAgo(minutesAgo: 35, seats: 2);

        await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Alerted(order, Times.Once());
    }

    /// <summary>
    /// One tick over the jobs the alert must leave alone — inside the half hour, already started,
    /// already alerted — and the one it must not: only that one is raised, and nothing is cancelled.
    /// </summary>
    [Fact]
    public async Task Only_The_Unstarted_Job_Past_The_Half_Hour_And_Not_Yet_Raised_Is_Alerted()
    {
        var due = ArrangeStartedAgo(minutesAgo: 35);
        var early = Started("order-early", minutesAgo: 20, OrderStatus.Confirmed);
        var started = Started("order-started", minutesAgo: 35, OrderStatus.InProgress);
        var raised = Started("order-raised", minutesAgo: 35, OrderStatus.Confirmed);
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { due, early, started, raised }.AsQueryable().BuildMock());
        _userNotifications
            .Setup(r => r.AnyForEventIgnoringTenantAsync(
                "tenant-a", AdminNotificationEventCatalog.OrderCleanerNotStarted, "orderId", raised.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.NotStartedAlerts);
        Alerted(due, Times.Once());
        _adminNotifier.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Subject != due.Id), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(OrderStatus.Confirmed, due.CurrentStatus);
        Assert.Null(due.CancelledAt);
    }

    private static Order Started(string orderId, int minutesAgo, OrderStatus status)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            orderId, status, maxEmployees: 1, cleaningDateTime: DateTime.UtcNow.AddMinutes(-minutesAgo));
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee($"emp-{orderId}", ContractStatus.Approved)));
        order.TenantId = "tenant-a";
        return order;
    }

    /// <summary>
    /// A job nobody has taken, under a day from its start: offerable (cash, so no payment is awaited, and a
    /// three-hour slot, so one already past its start is still open and only the window can refuse it), no
    /// reservation, and its status rows hours old, past the quiet period.
    /// </summary>
    private static Order UnstaffedJob(
        string orderId,
        double hoursOut = 20,
        OrderStatus status = OrderStatus.New,
        PaymentType paymentType = PaymentType.Cash,
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        string? recurringTemplateId = null,
        string? tenantId = "tenant-a")
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            orderId, status, maxEmployees: 1, paymentType, paymentStatus, recurringTemplateId,
            cleaningDateTime: DateTime.UtcNow.AddHours(hoursOut));
        order.UpdateEstimatedTime(180);
        foreach (var row in order.OrderStatusHistory)
        {
            row.Created("test", DateTimeOffset.UtcNow.AddHours(-3));
        }

        order.TenantId = tenantId;
        return order;
    }

    private void OnTheBoard(params Order[] orders)
    {
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(orders.AsQueryable().BuildMock());
        OnAJobRightNow();
    }

    private void NoCleanerAlerted(Order order, Times times) =>
        _adminNotifier.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Key == AdminNotificationEventCatalog.OrderNoCleanerYet && e.Subject == order.Id),
            It.IsAny<CancellationToken>()), times);

    private async Task AssertNoCleanerAlertIsNotRaisedAsync(params Order[] orders)
    {
        OnTheBoard(orders);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.NoCleanerAlerts);
        _adminNotifier.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Key == AdminNotificationEventCatalog.OrderNoCleanerYet),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task A_Job_Nobody_Has_Taken_Under_A_Day_From_Its_Start_Is_Reported_To_Its_Company(OrderStatus status)
    {
        var order = UnstaffedJob("order-unstaffed", status: status);
        OnTheBoard(order);
        AdminEvent? raised = null;
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((adminEvent, _) => raised = adminEvent)
            .Returns(Task.CompletedTask);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.NoCleanerAlerts);
        _adminNotifier.Verify(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderNoCleanerYet, raised.Key);
        Assert.Equal("tenant-a", raised.TenantId);
        Assert.Equal(order.Id, raised.Subject);
        Assert.Equal(3, raised.Args.Count);
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal(
            DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            raised.Args["cleaningDateTime"]);
        Assert.Equal(order.Id, raised.Args["orderId"]);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(-0.5)]
    public async Task A_Job_More_Than_A_Day_Out_Or_Past_Its_Start_Is_Not_Reported(double hoursOut)
    {
        await AssertNoCleanerAlertIsNotRaisedAsync(UnstaffedJob("order-unstaffed", hoursOut));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public async Task A_Job_With_Anybody_On_It_Is_Not_Reported_However_Full_Its_Crew_Or_Whoever_Asked_For_Cover(
        int seats, bool coverRequested)
    {
        var order = UnstaffedJob("order-staffed", status: OrderStatus.Confirmed);
        order.SetMaxEmployees(seats);
        var seat = OrderEmployee.Create(order, ValidatorTestHelpers.BuildEmployee("emp-on-it", ContractStatus.Approved));
        if (coverRequested)
        {
            seat.MarkCoverRequested(DateTime.UtcNow.AddHours(-2));
        }

        order.AddAssignedEmployee(seat);

        await AssertNoCleanerAlertIsNotRaisedAsync(order);
    }

    [Fact]
    public async Task An_Abandoned_Card_Checkout_And_An_Unconfirmed_Recurring_Cash_Occurrence_Are_Not_Reported()
    {
        await AssertNoCleanerAlertIsNotRaisedAsync(
            UnstaffedJob("order-unpaid-card", paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Pending),
            UnstaffedJob("order-unconfirmed-occurrence", recurringTemplateId: "template-1"));
    }

    [Fact]
    public async Task A_Paid_Card_Job_And_A_Confirmed_Recurring_Cash_Occurrence_Are_Reported()
    {
        var paid = UnstaffedJob("order-paid-card", paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);
        var confirmed = UnstaffedJob("order-confirmed-occurrence", recurringTemplateId: "template-1");
        confirmed.ConfirmByCustomer(DateTime.UtcNow.AddHours(-2));
        OnTheBoard(paid, confirmed);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(2, result.Value!.NoCleanerAlerts);
        NoCleanerAlerted(paid, Times.Once());
        NoCleanerAlerted(confirmed, Times.Once());
    }

    [Theory]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Only_A_Job_That_Never_Started_Is_Reported(OrderStatus status)
    {
        await AssertNoCleanerAlertIsNotRaisedAsync(UnstaffedJob("order-moved-on", status: status));
    }

    [Fact]
    public async Task A_Job_Still_Reserved_For_The_Customers_Chosen_Cleaner_Waits_For_The_Reservation_To_Lapse()
    {
        var now = DateTime.UtcNow;
        var reserved = UnstaffedJob("order-reserved");
        reserved.GrantPreferredHold("emp-chosen", untilUtc: now.AddHours(1), nowUtc: now, maxRounds: 2);
        var lapsed = UnstaffedJob("order-lapsed");
        lapsed.GrantPreferredHold("emp-chosen", untilUtc: now.AddHours(-1), nowUtc: now.AddHours(-3), maxRounds: 2);
        OnTheBoard(reserved, lapsed);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.NoCleanerAlerts);
        NoCleanerAlerted(lapsed, Times.Once());
        NoCleanerAlerted(reserved, Times.Never());
    }

    /// <summary>
    /// A booking, a drop's walk-back or re-advertisement and a rejection each write a fresh status row, and a
    /// customer's confirmation makes an occurrence takeable: the board and the seat-open push get an hour
    /// before the administrators hear about the job again.
    /// </summary>
    [Fact]
    public async Task A_Job_Whose_Status_Changed_Or_Which_The_Customer_Confirmed_Within_The_Hour_Is_Not_Reported_Yet()
    {
        var walkedBack = UnstaffedJob("order-walked-back");
        var row = OrderStatusTrack.Create(OrderStatus.New, walkedBack);
        row.Created("test", DateTimeOffset.UtcNow.AddMinutes(-30));
        walkedBack.AddOrderStatus(row);
        var justConfirmed = UnstaffedJob("order-just-confirmed", recurringTemplateId: "template-1");
        justConfirmed.ConfirmByCustomer(DateTime.UtcNow.AddMinutes(-30));

        await AssertNoCleanerAlertIsNotRaisedAsync(walkedBack, justConfirmed);
    }

    [Fact]
    public async Task A_Job_Already_Reported_Is_Not_Reported_Again()
    {
        var order = UnstaffedJob("order-reported");
        _userNotifications
            .Setup(r => r.AnyForEventIgnoringTenantAsync(
                "tenant-a", AdminNotificationEventCatalog.OrderNoCleanerYet, "orderId", order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await AssertNoCleanerAlertIsNotRaisedAsync(order);
    }

    [Fact]
    public async Task Each_Company_Is_Told_Under_Its_Own_Tenant_And_Committed_Before_The_Next()
    {
        OnTheBoard(
            UnstaffedJob("order-company-a", tenantId: "tenant-a"),
            UnstaffedJob("order-company-b", tenantId: "tenant-b"));
        var steps = new List<string>();
        _tenantProvider
            .Setup(t => t.SetTenantOverride(It.IsAny<string>()))
            .Callback<string>(tenant => steps.Add($"override {tenant}"));
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((adminEvent, _) => steps.Add($"raise {adminEvent.TenantId} {adminEvent.Subject}"))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("commit"))
            .Returns(Task.CompletedTask);

        var result = await Handler().Handle(new SendCleanerJobReminders.Command(), CancellationToken.None);

        Assert.Equal(2, result.Value!.NoCleanerAlerts);
        Assert.Equal(
            ["override tenant-a", "raise tenant-a order-company-a", "commit",
                "override tenant-b", "raise tenant-b order-company-b", "commit"],
            steps);
    }

    [Fact]
    public async Task A_Job_With_No_Company_Is_Skipped_Without_Reading_The_Feed()
    {
        await AssertNoCleanerAlertIsNotRaisedAsync(UnstaffedJob("order-no-company", tenantId: null));

        _userNotifications.Verify(r => r.AnyForEventIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(168, 240, true)]
    [InlineData(0, 60, false)]
    [InlineData(169, 60, false)]
    [InlineData(24, -1, false)]
    [InlineData(24, 241, false)]
    public void The_No_Cleaner_Lead_And_Quiet_Period_Are_Bounded(int leadHours, int quietMinutes, bool valid)
    {
        var result = new SendCleanerJobReminders.Validator().Validate(
            new SendCleanerJobReminders.Command(NoCleanerLeadHours: leadHours, NoCleanerQuietMinutes: quietMinutes));

        Assert.Equal(valid, result.IsValid);
    }
}

/// <summary>
/// A floor under the whole day-ahead digest: it decides who to message by converting UTC into the
/// cleaner's local hour, so an environment where IANA ids do not resolve would silently send every
/// digest at 18:00 UTC instead — the wrong hour for everyone, with nothing failing.
///
/// <para>Two things would cause that: <c>InvariantGlobalization=true</c> in a csproj, or an Alpine base
/// image without <c>tzdata</c>. Neither is true today; this is what notices if one becomes true.</para>
/// </summary>
public class TimeZoneDatabaseIsAvailableTests
{
    [Theory]
    [InlineData("Europe/Prague")]
    [InlineData("Europe/Bratislava")]
    [InlineData("Europe/Warsaw")]
    [InlineData("Europe/Berlin")]
    [InlineData("Europe/Vienna")]
    public void Every_Seeded_Country_Timezone_Resolves(string ianaId)
    {
        var zone = TimeZoneResolution.Resolve(ianaId);

        Assert.NotEqual(TimeZoneInfo.Utc, zone);
    }

    /// <summary>
    /// The offset must be real, not merely non-UTC — a resolver returning a zero-offset placeholder
    /// would pass the check above and still put every digest an hour out.
    /// </summary>
    [Fact]
    public void A_Resolved_Zone_Carries_A_Real_Offset()
    {
        var midsummer = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var prague = TimeZoneResolution.Resolve("Europe/Prague");

        Assert.Equal(TimeSpan.FromHours(2), prague.GetUtcOffset(midsummer));
    }

    [Fact]
    public void An_Unknown_Id_Still_Degrades_To_Utc_Rather_Than_Throwing()
    {
        Assert.Equal(TimeZoneInfo.Utc, TimeZoneResolution.Resolve("Not/AZone"));
        Assert.Equal(TimeZoneInfo.Utc, TimeZoneResolution.Resolve(null));
    }
}
