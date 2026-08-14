using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// EF-MODEL-METADATA guard for the order-seat arbiter.
///
/// THE HOLE: <c>OrderEmployee</c> was mapped entirely by convention — a key and two NON-unique indexes.
/// Nothing in the database bounded how many cleaners an order could carry. <c>TakeOrder</c> gates the
/// seat with three UNLOCKED reads (validator, handler re-check, and the in-memory guard in
/// <c>Order.AddAssignedEmployee</c>), so two cleaners tapping the same single-seat job both passed all
/// three and both inserted. Pay is one row per assigned employee, so the loser earned a second full wage
/// against an unchanged customer price — exactly what <c>BookingPolicy.SpareSeatsPerOrder = 0</c> exists
/// to prevent, reached by another route. Every other contested resource in this schema already carried a
/// SlotOrdinal unique index (<c>MembershipBenefitUsage</c>, <c>PromoCodeRedemption</c>); the order seat
/// was the only one without.
///
/// THE FIX (asserted here): a UNIQUE index over <c>(OrderId, SeatOrdinal)</c>, named so
/// the unique index on (OrderId, SeatOrdinal) can map its violation back to
/// the ordinary <c>order.no_available_spots</c> refusal instead of a 500.
///
/// The name assertion is the load-bearing one: <c>AppServices</c> deliberately cannot reference
/// <c>Infra.Database</c>, so a rename on the other side of that seam would silently turn a mapped
/// business error back into a 500 with nothing going red — the same reason
/// <c>UserIdentityLookupIndexTests</c> pins the users index name.
/// </summary>
public sealed class OrderEmployeeSeatOrdinalIndexTests
{
    private static IEntityType GetOrderEmployeeEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(OrderEmployee));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static bool IsCompositeIndexOn(IIndex index, params string[] propertyNames) =>
        index.Properties.Count == propertyNames.Length
        && index.Properties.Select(p => p.Name).SequenceEqual(propertyNames);

    [Fact]
    public void OrderEmployee_Has_Exactly_One_Unique_Index_Over_OrderId_And_SeatOrdinal()
    {
        var seatIndexes = GetOrderEmployeeEntityType()
            .GetIndexes()
            .Where(i => IsCompositeIndexOn(i, nameof(OrderEmployee.OrderId), nameof(OrderEmployee.SeatOrdinal)))
            .ToList();

        var seatIndex = Assert.Single(seatIndexes);
        Assert.True(seatIndex.IsUnique, "The seat index must be UNIQUE — it is the only thing that separates two concurrent takes.");
    }

    /// <summary>
    /// The name crosses an assembly seam that the compiler does not check. If this fails, a mapped
    /// business refusal has silently become a 500.
    /// </summary>
    [Fact]
    public void The_Seat_Index_Is_Unique_So_Two_Simultaneous_Takes_Cannot_Both_Land()
    {
        var seatIndex = GetOrderEmployeeEntityType()
            .GetIndexes()
            .Single(i => IsCompositeIndexOn(i, nameof(OrderEmployee.OrderId), nameof(OrderEmployee.SeatOrdinal)));

        Assert.True(seatIndex.IsUnique);
    }

    /// <summary>
    /// Smallest-FREE, not a count. Releasing seat 0 of {0,1} and deriving from a count would give 1,
    /// which is already taken — the seat would be permanently unusable and the order would read as full
    /// while a seat was free.
    /// </summary>
    [Fact]
    public void A_Released_Seat_Is_Reused_Rather_Than_Leaving_A_Hole()
    {
        // BuildOrder seats "emp-a" itself, at ordinal 0.
        var order = ValidatorTestHelpers.BuildOrder(
            "order-seat-1", OrderStatus.Confirmed, "emp-a", maxEmployees: 2);

        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-b")));
        Assert.Equal([0, 1], order.AssignedEmployees.Select(oe => oe.SeatOrdinal).OrderBy(o => o));

        order.UnassignEmployee("emp-a");
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-c")));

        Assert.Equal([0, 1], order.AssignedEmployees.Select(oe => oe.SeatOrdinal).OrderBy(o => o));
        Assert.Equal(0, order.AssignedEmployees.Single(oe => oe.EmployeeId == "emp-c").SeatOrdinal);
    }

    private static Employee NewEmployee(string employeeId)
    {
        var user = User.CreateWithPassword($"{employeeId}@example.com", "x", "Emp", "Loyee");
        user.Id = $"{employeeId}-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }

    /// <summary>Mirrors <c>UserIdentityLookupIndexTests</c>' provider (null ⇒ anonymous / no JWT).</summary>
    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
