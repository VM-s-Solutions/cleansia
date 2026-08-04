using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0036 D5.1 + ADR-0039 D2 — the resolver decides two things (notify, hold) and returns why.
/// The standing invariant is one-way: <c>HoldUntilUtc != null ⇒ NotifyPreferred</c>. Its converse does
/// not hold — a short lead grants the notification and refuses the hold, which is what lets the perk
/// mean something in the band where exclusivity is unsafe.
/// </summary>
public class PreferredCleanerHoldResolverTests
{
    private const string UserId = "user-booking-1";
    private const string CleanerId = "employee-preferred-1";
    private const string CleanerUserId = "user-cleaner-1";
    private const string CountryId = "country-cz";

    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CleaningUtc = Now.AddHours(48);
    private const int EstimatedMinutes = 180;

    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserNotificationPreferencesRepository> _preferences = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public PreferredCleanerHoldResolverTests()
    {
        _memberships
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewMembership());
        _employees
            .Setup(r => r.GetByIdAsync(CleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewCleaner());
        _preferences
            .Setup(r => r.GetByUserIdAsync(CleanerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotificationPreferences?)null);
        _devices
            .Setup(r => r.GetByUserIdAsync(CleanerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([NewDevice(notificationsEnabled: true)]);
        _orders
            .Setup(r => r.GetBusyEmployeeIdsInWindowAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
    }

    [Fact]
    public async Task An_Order_With_No_Preference_Gets_Neither()
    {
        var outcome = await ResolveAsync(preferredEmployeeId: null);

        AssertDeclined(outcome, HoldDeclineReason.NoPreference);
    }

    [Fact]
    public async Task A_Guest_Booking_Gets_Neither()
    {
        var outcome = await ResolveAsync(userId: null);

        AssertDeclined(outcome, HoldDeclineReason.NoMembership);
    }

    [Fact]
    public async Task A_Non_Member_Gets_Neither()
    {
        _memberships
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.NoMembership);
    }

    [Fact]
    public async Task An_Unknown_Cleaner_Gets_Neither()
    {
        _employees
            .Setup(r => r.GetByIdAsync(CleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerNotFound);
    }

    [Fact]
    public async Task An_Unapproved_Cleaner_Gets_Neither()
    {
        var cleaner = NewCleaner();
        cleaner.UpdateContractStatus(ContractStatus.Pending);
        _employees
            .Setup(r => r.GetByIdAsync(CleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cleaner);

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerNotApproved);
    }

    /// <summary>
    /// A departing or GDPR-erased cleaner is soft-deleted — <c>Deactivated(...)</c> sets
    /// <c>IsActive = false</c> and leaves <c>ContractStatus</c> untouched — so an approval gate reading
    /// the contract status alone hands the only seat, plus a targeted push, to someone who left the
    /// platform. That is 100% of the seat's fill window on a zero-probability outcome.
    /// </summary>
    [Fact]
    public async Task A_Departed_Cleaner_Who_Is_Still_Contractually_Approved_Gets_Neither()
    {
        var cleaner = NewCleaner();
        cleaner.Deactivated("gdpr", DateTimeOffset.UtcNow);
        _employees
            .Setup(r => r.GetByIdAsync(CleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cleaner);

        Assert.Equal(ContractStatus.Approved, cleaner.ContractStatus);
        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerNotApproved);
    }

    [Fact]
    public async Task A_Cleaner_Working_In_Another_Country_Gets_Neither()
    {
        AssertDeclined(
            await ResolveAsync(serviceCountryId: "country-sk"),
            HoldDeclineReason.CleanerCountryMismatch);
    }

    [Fact]
    public async Task A_Cleaner_Who_Muted_New_Jobs_Gets_Neither()
    {
        var preferences = UserNotificationPreferences.CreateDefaults(CleanerUserId);
        preferences.Set(NotificationCategory.NewJobsAvailable, enabled: false);
        _preferences
            .Setup(r => r.GetByUserIdAsync(CleanerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerMutedNewJobs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Cleaner_We_Cannot_Reach_Gets_Neither(bool hasADeviceRow)
    {
        _devices
            .Setup(r => r.GetByUserIdAsync(CleanerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasADeviceRow ? [NewDevice(notificationsEnabled: false)] : []);

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerUnreachableForPush);
    }

    /// <summary>
    /// The band below the floor keeps the weaker half of the perk: a hold nobody has time to act on is
    /// pure latency, but one outbox row still honours "first chance".
    /// </summary>
    [Fact]
    public async Task A_Short_Lead_Booking_Notifies_But_Never_Holds()
    {
        var outcome = await ResolveAsync(cleaningUtc: Now.AddHours(7));

        Assert.True(outcome.NotifyPreferred);
        Assert.Null(outcome.HoldUntilUtc);
        Assert.Equal(HoldDeclineReason.ShortLeadTime, outcome.Reason);
    }

    [Fact]
    public async Task A_Busy_Cleaner_Gets_No_Hold_And_No_Push()
    {
        MarkCleanerBusy();

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerBusyAtCleaningTime);
    }

    /// <summary>
    /// Short lead means we cannot hold; busy means they cannot take. If the busy check ran after the
    /// lead-time gate, a busy cleaner on a short-lead booking would collect a push about work
    /// <c>TakeOrder</c> is always going to refuse them — noise on the one channel the mechanism depends
    /// on being worth reading.
    /// </summary>
    [Fact]
    public async Task A_Busy_Cleaner_On_A_Short_Lead_Booking_Still_Gets_No_Push()
    {
        MarkCleanerBusy();

        AssertDeclined(
            await ResolveAsync(cleaningUtc: Now.AddHours(7)),
            HoldDeclineReason.CleanerBusyAtCleaningTime);
    }

    [Fact]
    public async Task The_Busy_Question_Is_Asked_About_The_Bookings_Own_Window()
    {
        await ResolveAsync();

        _orders.Verify(
            r => r.GetBusyEmployeeIdsInWindowAsync(
                It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 1 && ids.Contains(CleanerId)),
                CleaningUtc,
                CleaningUtc.AddMinutes(EstimatedMinutes),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The busy check is the only gate costing a range scan, so the ordinary path — no preference, or a
    /// non-member — must never reach it.
    /// </summary>
    [Fact]
    public async Task A_Non_Member_Never_Pays_For_The_Busy_Query()
    {
        _memberships
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        await ResolveAsync();

        _orders.Verify(
            r => r.GetBusyEmployeeIdsInWindowAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_Eligible_Cleaner_Earns_A_Hold_Of_The_Policy_Window()
    {
        var outcome = await ResolveAsync();

        Assert.True(outcome.NotifyPreferred);
        Assert.Equal(HoldDeclineReason.None, outcome.Reason);
        Assert.Equal(Now.Add(BookingPolicy.ComputePreferredHold(CleaningUtc, Now)), outcome.HoldUntilUtc);
    }

    /// <summary>
    /// A perk fails closed; a booking never does. An unanswerable busy check is treated as busy, so the
    /// order is still created and the preference is still stored.
    /// </summary>
    [Fact]
    public async Task An_Unanswerable_Busy_Check_Declines_Instead_Of_Throwing()
    {
        _orders
            .Setup(r => r.GetBusyEmployeeIdsInWindowAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.Data.Sqlite.SqliteException("database is locked", 5));

        AssertDeclined(await ResolveAsync(), HoldDeclineReason.CleanerBusyAtCleaningTime);
    }

    private static void AssertDeclined(PreferredCleanerOutcome outcome, HoldDeclineReason expected)
    {
        Assert.False(outcome.NotifyPreferred);
        Assert.Null(outcome.HoldUntilUtc);
        Assert.Equal(expected, outcome.Reason);
    }

    private void MarkCleanerBusy() =>
        _orders
            .Setup(r => r.GetBusyEmployeeIdsInWindowAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { CleanerId });

    private Task<PreferredCleanerOutcome> ResolveAsync(
        string? userId = UserId,
        string? preferredEmployeeId = CleanerId,
        string? serviceCountryId = CountryId,
        DateTime? cleaningUtc = null) =>
        new PreferredCleanerHoldResolver(
                _memberships.Object,
                _employees.Object,
                _preferences.Object,
                _devices.Object,
                _orders.Object,
                NullLogger<PreferredCleanerHoldResolver>.Instance)
            .ResolveAsync(
                userId,
                preferredEmployeeId,
                serviceCountryId,
                cleaningUtc ?? CleaningUtc,
                EstimatedMinutes,
                Now,
                CancellationToken.None);

    private static UserMembership NewMembership()
    {
        return UserMembership.Create(
            UserId,
            membershipPlanId: "plan-plus",
            stripeSubscriptionId: "sub_test_preferred",
            currentPeriodStart: Now.AddDays(-10),
            currentPeriodEnd: Now.AddDays(20));
    }

    private static Employee NewCleaner()
    {
        var user = User.CreateWithPassword(
            "preferred@cleansia.test", "Test-password-1!", "Anna", "Preferred", UserProfile.Employee);
        user.Id = CleanerUserId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = CleanerId;
        employee.Approve(approvedByUserId: "admin");
        employee.AssignWorkCountry(CountryId);
        return employee;
    }

    private static Device NewDevice(bool notificationsEnabled)
    {
        var device = Device.Create(CleanerUserId, "ios", "token-1", "device-1");
        if (!notificationsEnabled)
        {
            device.UpdateNotificationsEnabled(false);
        }

        return device;
    }
}
