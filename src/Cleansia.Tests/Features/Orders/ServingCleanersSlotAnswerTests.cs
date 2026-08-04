using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0039 D4/D5 — the picker's slot answer: which window it asks about, and what a row carries when
/// the question was not answered.
///
/// <para>The window is the load-bearing assertion (TC-AVAIL-WINDOW-0). Its length is derived in SQL from
/// the customer's selection, while the write path sums the same catalog rows in memory through
/// <see cref="OrderDuration"/>. Two implementations, one definition — if they drift, the picker is
/// answering about a different job than the one being booked, and the picker's "available" and the
/// resolver's "busy" become both correct and contradictory.</para>
/// </summary>
public sealed class ServingCleanersSlotAnswerTests : IDisposable
{
    private const string CustomerId = "user-slot-customer";
    private const string CleanerId = "emp-slot-cleaner";
    private const string DeepCleanId = "service-deep-clean";
    private const string WindowsId = "service-windows";
    private const string BundleId = "package-bundle";
    private const string BundledIroningId = "service-ironing";

    private static readonly DateTime CleaningUtc = new(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();

    private (IReadOnlyCollection<string> EmployeeIds, DateTime Start, DateTime End)? _busyQuestion;
    private HashSet<string> _busyAnswer = [];

    public ServingCleanersSlotAnswerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        ArrangeActiveMembership();

        _orderRepository
            .Setup(r => r.GetBusyEmployeeIdsInWindowAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> ids, DateTime start, DateTime end, CancellationToken _) =>
            {
                _busyQuestion = (ids, start, end);
                return _busyAnswer;
            });
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task The_Window_Length_Is_The_Sum_The_Write_Path_Would_Persist()
    {
        await SeedAsync();

        await HandleAsync(new GetMyServingCleaners.Query(
            CleaningDateTimeUtc: CleaningUtc,
            SelectedServiceIds: [DeepCleanId, WindowsId],
            SelectedPackageIds: [BundleId]));

        var expectedMinutes = OrderDuration.EstimateMinutes(
            [NewService(DeepCleanId, 120), NewService(WindowsId, 45)],
            [NewBundle()]);

        var question = Assert.NotNull(_busyQuestion);
        Assert.Equal(CleaningUtc, question.Start);
        Assert.Equal(CleaningUtc.AddMinutes(expectedMinutes), question.End);
        Assert.Equal([CleanerId], question.EmployeeIds);
    }

    [Fact]
    public async Task A_Cleaner_In_The_Busy_Set_Is_Marked_Unavailable()
    {
        await SeedAsync();
        _busyAnswer = [CleanerId];

        var result = await HandleAsync(SlotQuery());

        Assert.False(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
    }

    [Fact]
    public async Task A_Cleaner_Absent_From_The_Busy_Set_Is_Marked_Available()
    {
        await SeedAsync();

        var result = await HandleAsync(SlotQuery());

        Assert.True(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
    }

    /// <summary>
    /// The clients that ship today send no slot, and both hide the picker on an empty list. Absence of
    /// an answer renders as an ordinary selectable row, never as an unavailable one.
    /// </summary>
    [Fact]
    public async Task A_Request_With_No_Slot_Is_Not_Answered_And_Costs_No_Query()
    {
        await SeedAsync();

        var result = await HandleAsync(new GetMyServingCleaners.Query());

        Assert.Null(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
        Assert.Null(_busyQuestion);
    }

    [Fact]
    public async Task A_Slot_With_No_Selection_Is_Not_Answered()
    {
        await SeedAsync();

        var result = await HandleAsync(new GetMyServingCleaners.Query(CleaningDateTimeUtc: CleaningUtc));

        Assert.Null(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
        Assert.Null(_busyQuestion);
    }

    /// <summary>
    /// The window end is derived from a caller-chosen selection, so without the write path's span cap
    /// the request carries an unbounded range parameter under another name — a binary-search primitive
    /// against a named worker's calendar.
    /// </summary>
    [Fact]
    public async Task A_Selection_Longer_Than_The_Platform_Will_Book_Is_Not_Answered()
    {
        await SeedAsync(deepCleanMinutes: BookingPolicy.MaxBookableOrderSpanMinutes + 1);

        var result = await HandleAsync(new GetMyServingCleaners.Query(
            CleaningDateTimeUtc: CleaningUtc,
            SelectedServiceIds: [DeepCleanId]));

        Assert.Null(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
        Assert.Null(_busyQuestion);
    }

    [Fact]
    public async Task A_Non_Member_Is_Never_Answered_And_Costs_No_Query()
    {
        await SeedAsync();
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _busyAnswer = [CleanerId];

        var result = await HandleAsync(SlotQuery());

        Assert.Null(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
        Assert.Null(_busyQuestion);
    }

    /// <summary>
    /// A degradation, not a lie: marking every cleaner unavailable would empty the picker on both
    /// clients, which is worse than the behaviour this feature replaces.
    /// </summary>
    [Fact]
    public async Task A_Check_That_Cannot_Run_Leaves_The_Row_Unmarked()
    {
        await SeedAsync();
        _orderRepository
            .Setup(r => r.GetBusyEmployeeIdsInWindowAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqliteException("database is locked", 5));

        var result = await HandleAsync(SlotQuery());

        Assert.Null(Assert.Single(result.Value!).IsAvailableForRequestedSlot);
    }

    private static GetMyServingCleaners.Query SlotQuery() =>
        new(CleaningDateTimeUtc: CleaningUtc, SelectedServiceIds: [DeepCleanId]);

    private async Task<Cleansia.Infra.Common.Validations.BusinessResult<IReadOnlyList<GetMyServingCleaners.Response>>>
        HandleAsync(GetMyServingCleaners.Query query)
    {
        await using var ctx = NewContext();
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new OrderRepository(ctx).GetQueryable());

        var result = await new GetMyServingCleaners.Handler(
            _orderRepository.Object,
            new ServiceRepository(ctx),
            new PackageRepository(ctx),
            _membershipRepository.Object,
            new TestUserSessionProvider(CustomerId, "slot-customer@cleansia.test"),
            NullLogger<GetMyServingCleaners.Handler>.Instance).Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result;
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider());

    private void ArrangeActiveMembership() =>
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: CustomerId,
                membershipPlanId: "plan-plus",
                stripeSubscriptionId: "sub_slot",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));

    private static Service NewService(string id, int estimatedMinutes)
    {
        var service = Service.Create("category-1", id, id, 500m, 100m, estimatedMinutes);
        service.Id = id;
        return service;
    }

    /// <summary>The bundle's own length is its included services' — a package has no estimate of its own.</summary>
    private static Package NewBundle()
    {
        var bundle = Package.Create("Bundle", "Bundle", 900m);
        bundle.Id = BundleId;
        bundle.AddService(NewService(BundledIroningId, 90));
        return bundle;
    }

    private async Task SeedAsync(int deepCleanMinutes = 120)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(NewService(DeepCleanId, deepCleanMinutes));
        ctx.Add(NewService(WindowsId, 45));
        ctx.Add(NewBundle());

        var user = Cleansia.Core.Domain.Users.User.CreateWithPassword(
            "slot-cleaner@cleansia.test", "Test-password-1!", "Sofia", "Slot", UserProfile.Employee);
        user.Id = "user-slot-cleaner";
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var employee = Cleansia.Core.Domain.Users.Employee.CreateWithUser(user);
        employee.Id = CleanerId;
        employee.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        ctx.Add(employee);

        var order = Order.Create(
            customerName: "Slot Customer",
            customerEmail: "slot-customer@cleansia.test",
            customerPhone: "+420777555666",
            customerAddress: Cleansia.Core.Domain.Users.Address.Create("Slot St 2", "Praha", "14000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(-3),
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId);
        order.Id = "slot-order-completed";
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-4));
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));

        var stamp = DateTimeOffset.UtcNow.AddDays(-3);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed, OrderStatus.Completed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("system", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(20);
        }

        ctx.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
