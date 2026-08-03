using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0039 D12.1 / D12.3 — the two server-side gates the preferred-cleaner picker's feed must carry
/// BEFORE the slot-availability flag can be computed (A3), plus the A6 projection they ship inside.
///
/// <para><b>D12.1 — the perk is Plus-only, and today that is enforced only in the mobile UI.</b> The
/// entitlement moves onto the FLAG, never onto the list: a non-member gets every row they got
/// yesterday with <c>IsAvailableForRequestedSlot == null</c> ("not evaluated"), because gating the
/// list would empty a shipped contract and both clients render an empty list as *no picker at
/// all*.</para>
///
/// <para><b>D12.3 — a departed cleaner is soft-deleted, not removed</b>
/// (<c>GdprDeletionService</c> → <c>Deactivated(...)</c> → <c>IsActive = false</c>) while their
/// historical Completed order survives, and there is no global <c>IsActive</c> filter (S10). Having
/// no live orders they would compute as FREE the day the flag ships — an unmarked, selectable row
/// that earns the exclusive hold. The filter reads <c>IsActive</c> on the <c>Employee</c> AND on its
/// <c>User</c>, because the two flags are set by different paths.</para>
///
/// <para><b>A6 — the feed materialized 127 columns to use 4</b>, tracked, including
/// <c>Employees.IBAN</c> and <c>Employees.PassportId</c> into a customer-facing handler. The SQL
/// case below pins that those columns are no longer read at all.</para>
///
/// Rows are seeded through a real <see cref="CleansiaDbContext"/> over SQLite (mirroring
/// <see cref="ColdPathCurrentStatusQueryTests"/>) so the filter and the projection are proven as
/// translated SQL, not as an in-memory LINQ afterthought.
/// </summary>
public sealed class ServingCleanersPickerGateTests : IDisposable
{
    private const string CustomerId = "user-picker-customer";

    private readonly SqliteConnection _connection;
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();

    public ServingCleanersPickerGateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    public void Dispose() => _connection.Dispose();

    // ── D12.1: the gate itself, as pure logic. ───────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Non_Member_Never_Receives_A_Computed_Availability_Answer(bool evaluated)
    {
        Assert.Null(GetMyServingCleaners.ResolveSlotAvailability(hasActiveMembership: false, evaluated));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Member_Receives_The_Computed_Availability_Answer_Verbatim(bool evaluated)
    {
        Assert.Equal(evaluated, GetMyServingCleaners.ResolveSlotAvailability(hasActiveMembership: true, evaluated));
    }

    [Fact]
    public void A_Member_With_Nothing_Computed_Stays_Not_Evaluated()
    {
        Assert.Null(GetMyServingCleaners.ResolveSlotAvailability(hasActiveMembership: true, evaluatedAvailability: null));
    }

    // ── D12.1: the gate lands on the flag, and never on the list. ────────────────────────────────

    [Fact]
    public async Task A_Non_Member_Gets_The_Rows_With_A_Null_Flag_Not_An_Empty_List()
    {
        await SeedOneServingCleanerAsync();

        var result = await HandleAsync();

        Assert.True(result.IsSuccess);
        var cleaner = Assert.Single(result.Value!);
        Assert.Equal("emp-picker-live", cleaner.EmployeeId);
        Assert.Null(cleaner.IsAvailableForRequestedSlot);
    }

    [Fact]
    public async Task A_Member_Gets_The_Same_Rows_As_A_Non_Member()
    {
        await SeedOneServingCleanerAsync();
        ArrangeActiveMembership();

        var result = await HandleAsync();

        Assert.True(result.IsSuccess);
        var cleaner = Assert.Single(result.Value!);
        Assert.Equal("emp-picker-live", cleaner.EmployeeId);
    }

    [Fact]
    public async Task The_Membership_Gate_Is_Resolved_For_The_Session_User_On_Every_Request()
    {
        await SeedOneServingCleanerAsync();

        await HandleAsync();

        _membershipRepository.Verify(
            r => r.GetActiveForUserNoTrackingAsync(CustomerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── D12.3: a cleaner who left the platform is not offered. ───────────────────────────────────

    [Fact]
    public async Task A_Deactivated_Employee_Is_Not_Offered()
    {
        await SeedOneServingCleanerAsync(deactivateEmployee: true);

        var result = await HandleAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task A_Cleaner_Whose_User_Was_Deactivated_Is_Not_Offered()
    {
        await SeedOneServingCleanerAsync(deactivateUser: true);

        var result = await HandleAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task A_Live_Cleaner_Is_Still_Offered_When_A_Departed_One_Shares_The_History()
    {
        await SeedOneServingCleanerAsync();
        await SeedSecondServingCleanerAsync(deactivateEmployee: true);

        var result = await HandleAsync();

        Assert.True(result.IsSuccess);
        var cleaner = Assert.Single(result.Value!);
        Assert.Equal("emp-picker-live", cleaner.EmployeeId);
    }

    // ── A6: four columns, none of them the cleaner's bank or identity documents. ──────────────────

    [Fact]
    public async Task The_Feed_Never_Reads_The_Cleaners_Bank_Or_Identity_Columns()
    {
        await SeedOneServingCleanerAsync();
        var sql = new List<string>();

        await HandleAsync(sql);

        var emitted = string.Join("\n", sql);
        Assert.DoesNotContain("IBAN", emitted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PassportId", emitted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NationalityId", emitted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VatNumber", emitted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RegistrationNumber", emitted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FirstName", emitted, StringComparison.Ordinal);
        Assert.Contains("LIMIT", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Feed_Tracks_Nothing()
    {
        await SeedOneServingCleanerAsync();

        await using var ctx = NewContext();
        await NewHandler(ctx).Handle(new GetMyServingCleaners.Query(), CancellationToken.None);

        Assert.Empty(ctx.ChangeTracker.Entries());
    }

    // ── Arrange ──────────────────────────────────────────────────────────────────────────────────

    private CleansiaDbContext NewContext(ICollection<string>? sqlSink = null)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection);
        if (sqlSink is not null)
            options.LogTo(sqlSink.Add, new[] { RelationalEventId.CommandExecuted });

        return new CleansiaDbContext(
            options.Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
    }

    private GetMyServingCleaners.Handler NewHandler(CleansiaDbContext ctx) =>
        new(
            new OrderRepository(ctx),
            _membershipRepository.Object,
            new TestUserSessionProvider(CustomerId, "picker-customer@cleansia.test"));

    private async Task<Cleansia.Infra.Common.Validations.BusinessResult<IReadOnlyList<GetMyServingCleaners.Response>>>
        HandleAsync(ICollection<string>? sqlSink = null)
    {
        await using var ctx = NewContext(sqlSink);
        return await NewHandler(ctx).Handle(new GetMyServingCleaners.Query(), CancellationToken.None);
    }

    private void ArrangeActiveMembership()
    {
        var membership = UserMembership.Create(
            userId: CustomerId,
            membershipPlanId: "plan-plus",
            stripeSubscriptionId: "sub_picker",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    private Task SeedOneServingCleanerAsync(bool deactivateEmployee = false, bool deactivateUser = false) =>
        SeedCompletedOrderAsync(
            "picker-order-live", "emp-picker-live", "user-picker-live", "Lena", "Live",
            deactivateEmployee, deactivateUser);

    private Task SeedSecondServingCleanerAsync(bool deactivateEmployee = false, bool deactivateUser = false) =>
        SeedCompletedOrderAsync(
            "picker-order-gone", "emp-picker-gone", "user-picker-gone", "Gone", "Away",
            deactivateEmployee, deactivateUser);

    private async Task SeedCompletedOrderAsync(
        string orderId,
        string employeeId,
        string employeeUserId,
        string firstName,
        string lastName,
        bool deactivateEmployee,
        bool deactivateUser)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var user = User.CreateWithPassword(
            $"{employeeUserId}@cleansia.test", "Test-password-1!", firstName, lastName, UserProfile.Employee);
        user.Id = employeeUserId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        employee.UpdateBankDetails("CZ6508000000192000145399");
        employee.UpdateIdentification("country-cz", "PASS-123456");

        if (deactivateEmployee)
            employee.Deactivated("admin-picker", DateTimeOffset.UtcNow);
        if (deactivateUser)
            user.Deactivated("admin-picker", DateTimeOffset.UtcNow);

        ctx.Add(employee);

        var order = Order.Create(
            customerName: "Picker Customer",
            customerEmail: "picker-customer@cleansia.test",
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Picker St 1", "Praha", "14000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(-3),
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId);
        order.Id = orderId;
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-4));
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));

        var stamp = DateTimeOffset.UtcNow.AddDays(-3);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("system", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(20);
        }

        ctx.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
