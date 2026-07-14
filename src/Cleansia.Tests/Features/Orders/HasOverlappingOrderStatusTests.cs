using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <see cref="OrderRepository.HasOverlappingOrderAsync"/> backs the TakeOrder
/// <c>order.time_conflict</c> rule and the new-jobs digest's not-busy filter. An assigned order
/// occupies the cleaner's time only while it is a live commitment — terminal orders (Completed,
/// Cancelled) free the slot. These tests run the real predicate against a
/// <see cref="CleansiaDbContext"/> over SQLite with rows seeded through the
/// <see cref="Order.AddOrderStatus"/> seam, pinning the per-status decision, the window/assignee
/// semantics, and the pre-backfill NULL-column fallback to the latest-history rule
/// (CreatedOn desc, Sequence desc).
/// </summary>
public sealed class HasOverlappingOrderStatusTests : IDisposable
{
    private const string CleanerId = "emp-overlap";
    private static readonly DateTime SlotStart = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;

    public HasOverlappingOrderStatusTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    // ── The status decision: a live commitment blocks the slot. ──

    [Fact]
    public async Task New_Order_Conflicts_An_Assigned_Booking_Claims_The_Slot_Even_Before_Confirmation()
    {
        await SeedAssignedOrderInSlotAsync("ovl-new", [OrderStatus.New]);

        Assert.True(await ProbeSlotAsync());
    }

    [Fact]
    public async Task Pending_Order_Conflicts_Payment_In_Flight_Still_Proceeds_To_The_Job()
    {
        await SeedAssignedOrderInSlotAsync("ovl-pending", [OrderStatus.New, OrderStatus.Pending]);

        Assert.True(await ProbeSlotAsync());
    }

    [Fact]
    public async Task Confirmed_Order_Conflicts_The_Cleaner_Committed_To_Show_Up()
    {
        await SeedAssignedOrderInSlotAsync("ovl-confirmed", [OrderStatus.New, OrderStatus.Confirmed]);

        Assert.True(await ProbeSlotAsync());
    }

    [Fact]
    public async Task OnTheWay_Order_Conflicts_The_Cleaner_Is_En_Route_To_It()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-ontheway", [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay]);

        Assert.True(await ProbeSlotAsync());
    }

    [Fact]
    public async Task InProgress_Order_Conflicts_The_Cleaner_Is_Working_It()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-inprogress", [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress]);

        Assert.True(await ProbeSlotAsync());
    }

    // ── Terminal orders are no commitment: the slot is free again. ──

    [Fact]
    public async Task Completed_Order_Does_Not_Conflict_Finished_Work_Frees_The_Slot()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-completed",
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed]);

        Assert.False(await ProbeSlotAsync());
    }

    [Fact]
    public async Task Cancelled_Order_Does_Not_Conflict_A_Dead_Booking_Holds_No_Time()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-cancelled", [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.Cancelled]);

        Assert.False(await ProbeSlotAsync());
    }

    // ── Pre-backfill NULL column: fall back to the authoritative latest-history rule.
    //    Excluding NULL rows here would fail OPEN (an active legacy order stops blocking
    //    and the cleaner gets double-booked), so this filter must fall back, not exclude. ──

    [Fact]
    public async Task Null_CurrentStatus_Falls_Back_To_Latest_History_A_Legacy_Confirmed_Order_Still_Conflicts()
    {
        await SeedAssignedOrderInSlotAsync("ovl-legacy-active", [OrderStatus.New, OrderStatus.Confirmed]);
        await NullOutCurrentStatusColumnAsync("ovl-legacy-active");

        Assert.True(await ProbeSlotAsync());
    }

    [Fact]
    public async Task Null_CurrentStatus_Fallback_Honors_The_Sequence_Tiebreak_A_Same_Tick_Cancellation_Frees_The_Slot()
    {
        await EnsureSchemaAsync();
        var stamp = DateTimeOffset.UtcNow.AddHours(-3);
        await using (var seed = NewContext())
        {
            var cleaner = NewEmployee(CleanerId);
            seed.Add(cleaner);

            var order = NewOrder("ovl-legacy-tiebreak", SlotStart, estimatedMinutes: 120);
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
            AppendTrack(order, OrderStatus.Confirmed, stamp);
            AppendTrack(order, OrderStatus.Cancelled, stamp);
            seed.Add(order);

            await seed.CommitAsync(CancellationToken.None);
        }
        await NullOutCurrentStatusColumnAsync("ovl-legacy-tiebreak");

        Assert.False(await ProbeSlotAsync());
    }

    // ── Window + assignee semantics stay exactly as before. ──

    [Fact]
    public async Task Active_Order_Outside_The_Window_Does_Not_Conflict()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-elsewhere",
            [OrderStatus.New, OrderStatus.Confirmed],
            cleaningDateTime: SlotStart.AddHours(5));

        Assert.False(await ProbeSlotAsync());
    }

    [Fact]
    public async Task Another_Cleaners_Active_Order_In_The_Window_Does_Not_Conflict()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-other-cleaner",
            [OrderStatus.New, OrderStatus.Confirmed],
            assignedTo: "emp-someone-else");

        Assert.False(await ProbeSlotAsync());
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// One order assigned to the cleaner, scheduled 10:00–12:00 in the probe slot by default,
    /// walked through the given statuses via the AddOrderStatus seam.
    /// </summary>
    private async Task SeedAssignedOrderInSlotAsync(
        string orderId,
        OrderStatus[] statuses,
        DateTime? cleaningDateTime = null,
        string assignedTo = CleanerId)
    {
        await EnsureSchemaAsync();
        await using var seed = NewContext();

        var cleaner = NewEmployee(assignedTo);
        seed.Add(cleaner);

        var order = NewOrder(orderId, cleaningDateTime ?? SlotStart, estimatedMinutes: 120);
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        AppendTracks(order, statuses);
        seed.Add(order);

        await seed.CommitAsync(CancellationToken.None);
    }

    /// <summary>Probe 10:30–11:30 inside the seeded 10:00–12:00 slot, as CleanerId.</summary>
    private async Task<bool> ProbeSlotAsync()
    {
        await using var ctx = NewContext();
        return await new OrderRepository(ctx).HasOverlappingOrderAsync(
            CleanerId, SlotStart.AddMinutes(30), 60, CancellationToken.None);
    }

    private async Task NullOutCurrentStatusColumnAsync(string orderId)
    {
        await using var ctx = NewContext();
        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE \"Orders\" SET \"CurrentStatus\" = NULL WHERE \"Id\" = {0}", orderId);
    }

    private static Order NewOrder(string orderId, DateTime cleaningDateTime, int estimatedMinutes)
    {
        var order = Order.Create(
            customerName: "Overlap Customer",
            customerEmail: "overlap-customer@cleansia.test",
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Overlap St 1", "Praha", "14000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid);
        order.Id = orderId;
        order.UpdateEstimatedTime(estimatedMinutes);
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-2));
        return order;
    }

    private static Employee NewEmployee(string employeeId)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test", "Test-password-1!", "Olga", "Overlap", UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        return employee;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private static void AppendTracks(Order order, params OrderStatus[] statuses)
    {
        var stamp = DateTimeOffset.UtcNow.AddHours(-6);
        foreach (var status in statuses)
        {
            AppendTrack(order, status, stamp);
            stamp = stamp.AddMinutes(20);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
