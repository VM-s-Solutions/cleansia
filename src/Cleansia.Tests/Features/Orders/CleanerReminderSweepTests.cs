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
/// <c>ceil(EstimatedTime / 120)</c>, so a 180-minute service — of which the seeded catalogue has
/// several — carries two cleaners. A reminder stamp on the ORDER would remind the first and silently
/// skip the second, which is why the stamps live on the assignment row.</para>
/// </summary>
public class CleanerReminderSweepTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private SendCleanerJobReminders.Handler Handler() => new(
        _orderRepository.Object,
        _producer.Object,
        _tenantProvider.Object,
        _unitOfWork.Object,
        NullLogger<SendCleanerJobReminders.Handler>.Instance);

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
