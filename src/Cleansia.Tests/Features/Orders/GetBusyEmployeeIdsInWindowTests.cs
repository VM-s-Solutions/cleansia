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
/// ADR-0039 D3 — the set-based form of "who is busy in this window". It answers for a LIST of
/// candidates in one query, because the picker asks about up to twenty cleaners per render and the
/// singular method called in a loop is twenty range scans on the booking hot path.
///
/// <para>The property that carries the most weight is not any single verdict: it is that the set form
/// and the boolean form give the SAME answer on the same rows. They share one private window filter, so
/// the day they stop agreeing is the day the extraction was undone — and the picker and the take gate
/// would be answering the same question differently.</para>
/// </summary>
public sealed class GetBusyEmployeeIdsInWindowTests : IDisposable
{
    private const string BusyCleaner = "emp-busy";
    private const string FreeCleaner = "emp-free";
    private const string UnknownCleaner = "emp-not-a-candidate";
    private const string SeedTenantId = "tenant-busy-1";
    private const string OtherTenantId = "tenant-busy-2";

    private static readonly DateTime SlotStart = new(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ProbeStart = SlotStart.AddMinutes(30);
    private const int ProbeMinutes = 60;

    private readonly SqliteConnection _connection;

    public GetBusyEmployeeIdsInWindowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Only_The_Busy_Candidates_Come_Back()
    {
        await SeedAsync("busy-subset", BusyCleaner, tenantId: null);

        var busy = await ProbeAsync([BusyCleaner, FreeCleaner]);

        Assert.Equal([BusyCleaner], busy);
    }

    /// <summary>
    /// The answer is the BUSY subset, never the free one, so an unknown or unqueried cleaner is absent
    /// and absence reads as available. That is what makes every failure of this check fail OPEN.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Outside_The_Candidate_Set_Is_Never_Reported()
    {
        await SeedAsync("busy-outside", UnknownCleaner, tenantId: null);

        Assert.Empty(await ProbeAsync([BusyCleaner, FreeCleaner]));
    }

    [Fact]
    public async Task An_Empty_Candidate_Set_Answers_Empty()
    {
        await SeedAsync("busy-empty-input", BusyCleaner, tenantId: null);

        Assert.Empty(await ProbeAsync([]));
    }

    [Theory]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task A_Terminal_Order_Hands_The_Slot_Back(OrderStatus terminal)
    {
        await SeedAsync("busy-terminal", BusyCleaner, tenantId: null, finalStatus: terminal);

        Assert.Empty(await ProbeAsync([BusyCleaner]));
    }

    [Fact]
    public async Task An_Order_That_Ends_Before_The_Window_Opens_Does_Not_Block()
    {
        await SeedAsync(
            "busy-before",
            BusyCleaner,
            tenantId: null,
            cleaningDateTime: ProbeStart.AddMinutes(-120),
            estimatedMinutes: 60);

        Assert.Empty(await ProbeAsync([BusyCleaner]));
    }

    [Fact]
    public async Task An_Order_Starting_Exactly_On_The_Scan_Floor_Still_Blocks()
    {
        await SeedAsync(
            "busy-floor-edge",
            BusyCleaner,
            tenantId: null,
            cleaningDateTime: ProbeStart.AddHours(-Order.MaxOrderSpanHours),
            estimatedMinutes: (Order.MaxOrderSpanHours * 60) + 30);

        Assert.Equal([BusyCleaner], await ProbeAsync([BusyCleaner]));
    }

    [Fact]
    public async Task Another_Tenants_Commitment_Is_Invisible()
    {
        await SeedAsync("busy-foreign-tenant", BusyCleaner, SeedTenantId);

        Assert.Empty(await ProbeAsync([BusyCleaner], callerTenantId: OtherTenantId));
        Assert.Equal([BusyCleaner], await ProbeAsync([BusyCleaner], callerTenantId: SeedTenantId));
    }

    /// <summary>
    /// One window filter, two terminal shapes. If these ever disagree, the picker's answer and the take
    /// gate's answer have come apart — which is the whole failure this feature exists to prevent.
    /// </summary>
    [Theory]
    [InlineData(0, 120, true)]
    [InlineData(-120, 60, false)]
    [InlineData(25, 5, true)]
    [InlineData(-(Order.MaxOrderSpanHours * 60), (Order.MaxOrderSpanHours * 60) + 30, true)]
    public async Task The_Set_Form_And_The_Boolean_Form_Agree(int startOffsetMinutes, int estimatedMinutes, bool expectedBusy)
    {
        await SeedAsync(
            "busy-agreement",
            BusyCleaner,
            tenantId: null,
            cleaningDateTime: ProbeStart.AddMinutes(startOffsetMinutes),
            estimatedMinutes: estimatedMinutes);

        var fromSet = await ProbeAsync([BusyCleaner]);

        await using var ctx = NewContext(tenantId: null);
        var fromBoolean = await new OrderRepository(ctx)
            .HasOverlappingOrderAsync(BusyCleaner, ProbeStart, ProbeMinutes, CancellationToken.None);

        Assert.Equal(expectedBusy, fromBoolean);
        Assert.Equal(fromBoolean, fromSet.Contains(BusyCleaner));
    }

    private async Task<IReadOnlySet<string>> ProbeAsync(string[] candidates, string? callerTenantId = null)
    {
        await using var ctx = NewContext(callerTenantId);
        return await new OrderRepository(ctx).GetBusyEmployeeIdsInWindowAsync(
            candidates, ProbeStart, ProbeStart.AddMinutes(ProbeMinutes), CancellationToken.None);
    }

    private CleansiaDbContext NewContext(string? tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    private async Task SeedAsync(
        string orderId,
        string assignedEmployeeId,
        string? tenantId,
        DateTime? cleaningDateTime = null,
        int estimatedMinutes = 120,
        OrderStatus finalStatus = OrderStatus.Confirmed)
    {
        await using (var schema = NewContext(tenantId: null))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext(tenantId);

        var cleaner = NewEmployee(assignedEmployeeId);
        seed.Add(cleaner);

        var order = NewOrder(orderId, cleaningDateTime ?? SlotStart, estimatedMinutes);
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddHours(-6));
        AppendTrack(order, finalStatus, DateTimeOffset.UtcNow.AddHours(-5));
        seed.Add(order);

        await seed.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string orderId, DateTime cleaningDateTime, int estimatedMinutes)
    {
        var order = Order.Create(
            customerName: "Busy Customer",
            customerEmail: "busy-set@cleansia.test",
            customerPhone: "+420777222444",
            customerAddress: Address.Create("Busy St 4", "Praha", "14000", "cz"),
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
            $"{employeeId}@cleansia.test", "Test-password-1!", "Bea", "Busy", UserProfile.Employee);
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
