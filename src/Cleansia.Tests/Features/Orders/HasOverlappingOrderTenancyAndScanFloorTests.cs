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
/// The overlap predicate answers "is this cleaner already busy at this time?" for two callers living in
/// OPPOSITE tenancy worlds — <c>TakeOrder</c>'s write gate (a request path, which has a tenant claim) and
/// the new-jobs digest sweep (a timer, which has none and reads
/// <c>GetQueryableIgnoringTenant</c>). One tenant-SCOPED method served both: under a tenant the filter
/// resolved <c>TenantId == null</c> against non-null rows, so every branch was false, the query returned
/// nothing, and every cleaner reported FREE — the digest would advertise clashing jobs and, because the
/// same method is the booking write gate, a genuine double-booking would be permitted (S8's third form,
/// ADR-0039 §D6). Invisible in single-tenant mode, so every tenancy case here seeds a NON-NULL
/// <c>TenantId</c>.
///
/// The second half pins the scan floor: the overlap predicate's only sargable term is the UPPER bound
/// (<c>CleaningDateTime &lt; windowEnd</c>) — the lower side is a per-row interval computation — so
/// without a floor the gate scanned a cleaner's entire lifetime assignment history. The floor is safe
/// exactly while no order spans longer than <see cref="Order.MaxOrderSpanHours"/>; these tests pin that
/// the longest order the catalog can actually produce is still caught, that the floor's edge is
/// inclusive, and what the floor's accepted blind spot is.
/// </summary>
public sealed class HasOverlappingOrderTenancyAndScanFloorTests : IDisposable
{
    private const string CleanerId = "emp-overlap-tenancy";
    private const string SeedTenantId = "tenant-overlap-1";
    private const string OtherTenantId = "tenant-overlap-2";

    /// <summary>
    /// The longest <see cref="Order.EstimatedTime"/> the shipped catalog can produce: every seeded
    /// service (1215 min) plus every seeded package's included services (2280 min) on one order —
    /// nothing caps the selection count (<c>CreateOrder.Validator</c> only checks existence) and nothing
    /// caps a service's estimate (<c>CreateService.Validator</c> is <c>GreaterThanOrEqualTo(0)</c>).
    /// 58.25 h, which is why the scan floor may not be a day.
    /// </summary>
    private const int LongestProducibleOrderMinutes = 3495;

    private static readonly DateTime SlotStart = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Every probe asks about 10:30–11:30, so the scan floor is measured back from 10:30.</summary>
    private static readonly DateTime ProbeStart = SlotStart.AddMinutes(30);

    private readonly SqliteConnection _connection;

    public HasOverlappingOrderTenancyAndScanFloorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    // ── Tenancy: one question, two worlds. ──

    [Fact]
    public async Task Tenanted_Overlap_Is_Detected_By_The_Sweeps_Ignoring_Variant()
    {
        await SeedAssignedOrderInSlotAsync("ovl-tenant-sweep", SeedTenantId);

        Assert.True(await ProbeIgnoringTenantAsync(callerTenantId: null));
    }

    [Fact]
    public async Task Tenanted_Overlap_Is_Detected_By_The_Write_Gate_Under_The_Same_Tenant()
    {
        await SeedAssignedOrderInSlotAsync("ovl-tenant-gate", SeedTenantId);

        Assert.True(await ProbeScopedAsync(callerTenantId: SeedTenantId));
    }

    [Fact]
    public async Task Another_Tenants_Overlap_Is_Invisible_To_The_Write_Gate()
    {
        await SeedAssignedOrderInSlotAsync("ovl-tenant-foreign", SeedTenantId);

        Assert.False(await ProbeScopedAsync(callerTenantId: OtherTenantId));
    }

    /// <summary>
    /// The reason the sibling has to exist, pinned rather than described: the scoped variant is correct
    /// for a request path and silently answers "free" for a tenanted cleaner when the caller carries no
    /// claim. A background sweep that calls it gets a safe-sounding lie.
    /// </summary>
    [Fact]
    public async Task The_Scoped_Variant_Reports_Free_For_A_Tenanted_Order_When_The_Caller_Has_No_Claim()
    {
        await SeedAssignedOrderInSlotAsync("ovl-tenant-lie", SeedTenantId);

        Assert.False(await ProbeScopedAsync(callerTenantId: null));
    }

    [Fact]
    public async Task Single_Tenant_Rows_Stay_Visible_To_Both_Variants()
    {
        await SeedAssignedOrderInSlotAsync("ovl-tenant-null", tenantId: null);

        Assert.True(await ProbeScopedAsync(callerTenantId: null));
        Assert.True(await ProbeIgnoringTenantAsync(callerTenantId: null));
    }

    // ── The scan floor: it may cost a wider scan, it may not lose an overlap. ──

    /// <summary>
    /// The longest order the platform can actually produce, starting early enough that a floor of a day
    /// would drop it, running into the probe window. A floor tighter than the producible span makes this
    /// overlap invisible ON THE WRITE GATE — a double booking.
    /// </summary>
    [Fact]
    public async Task The_Longest_Producible_Order_Still_Conflicts_When_It_Runs_Into_The_Window()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-floor-longest",
            tenantId: null,
            cleaningDateTime: SlotStart.AddMinutes(-(LongestProducibleOrderMinutes - 45)),
            estimatedMinutes: LongestProducibleOrderMinutes);

        Assert.True(await ProbeScopedAsync(callerTenantId: null));
    }

    /// <summary>The floor's edge is inclusive — an order starting exactly on it is still evaluated.</summary>
    [Fact]
    public async Task An_Order_Starting_Exactly_On_The_Scan_Floor_Still_Conflicts()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-floor-edge",
            tenantId: null,
            cleaningDateTime: ProbeStart.AddHours(-Order.MaxOrderSpanHours),
            estimatedMinutes: (Order.MaxOrderSpanHours * 60) + 30);

        Assert.True(await ProbeScopedAsync(callerTenantId: null));
    }

    /// <summary>
    /// The floor's accepted blind spot, pinned so it is a recorded trade rather than a surprise: an order
    /// that both starts before the floor AND spans longer than <see cref="Order.MaxOrderSpanHours"/> is
    /// not evaluated. Nothing produces such an order today — the catalog's maximum is
    /// <see cref="LongestProducibleOrderMinutes"/>, under a third of the floor — but nothing ENFORCES
    /// that either, which is why the span cap belongs on the order write path (or the appointment's end
    /// instant belongs in a column, ADR-0039 A15).
    /// </summary>
    [Fact]
    public async Task An_Order_Longer_Than_The_Span_Bound_Starting_Before_The_Floor_Is_Outside_The_Guarantee()
    {
        await SeedAssignedOrderInSlotAsync(
            "ovl-floor-beyond",
            tenantId: null,
            cleaningDateTime: ProbeStart.AddHours(-Order.MaxOrderSpanHours).AddMinutes(-1),
            estimatedMinutes: (Order.MaxOrderSpanHours * 60) + 30);

        Assert.False(await ProbeScopedAsync(callerTenantId: null));
    }

    private CleansiaDbContext NewContext(string? tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    /// <summary>One live-commitment order assigned to the cleaner, seeded through a tenant-carrying context.</summary>
    private async Task SeedAssignedOrderInSlotAsync(
        string orderId,
        string? tenantId,
        DateTime? cleaningDateTime = null,
        int estimatedMinutes = 120)
    {
        await using (var schema = NewContext(tenantId: null))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using (var seed = NewContext(tenantId))
        {
            var cleaner = NewEmployee(CleanerId);
            seed.Add(cleaner);

            var order = NewOrder(orderId, cleaningDateTime ?? SlotStart, estimatedMinutes);
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
            AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddHours(-6));
            AppendTrack(order, OrderStatus.Confirmed, DateTimeOffset.UtcNow.AddHours(-5));
            seed.Add(order);

            await seed.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext(tenantId: null);
        var stamped = await verify.Set<Order>().IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        Assert.Equal(tenantId, stamped.TenantId);
    }

    /// <summary>Probe 10:30–11:30 inside the 10:00 slot, as CleanerId, through the tenant-SCOPED gate.</summary>
    private async Task<bool> ProbeScopedAsync(string? callerTenantId)
    {
        await using var ctx = NewContext(callerTenantId);
        return await new OrderRepository(ctx).HasOverlappingOrderAsync(
            CleanerId, SlotStart.AddMinutes(30), 60, CancellationToken.None);
    }

    /// <summary>The same probe through the sweep's tenant-IGNORING sibling.</summary>
    private async Task<bool> ProbeIgnoringTenantAsync(string? callerTenantId)
    {
        await using var ctx = NewContext(callerTenantId);
        return await new OrderRepository(ctx).HasOverlappingOrderIgnoringTenantAsync(
            CleanerId, SlotStart.AddMinutes(30), 60, CancellationToken.None);
    }

    private static Order NewOrder(string orderId, DateTime cleaningDateTime, int estimatedMinutes)
    {
        var order = Order.Create(
            customerName: "Overlap Customer",
            customerEmail: "overlap-tenancy@cleansia.test",
            customerPhone: "+420777333555",
            customerAddress: Address.Create("Overlap St 2", "Praha", "14000", "cz"),
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

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
