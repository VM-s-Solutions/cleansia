using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The seat arithmetic a cover request introduces.
///
/// <para>Owner ruling 2026-09-06: a cleaner who cannot make a job asks for cover and <b>stays
/// assigned</b> until somebody takes it. That makes "how many seats are unfilled" and "may another
/// cleaner take one" two different questions for the first time — they had the same answer for as
/// long as an assignment could only be added or hard-deleted.</para>
///
/// <para>Getting this wrong in either direction is expensive. Conflate them upward and a cover
/// request stops the seat being offered at all, which is the whole feature failing silently.
/// Conflate them downward and <c>AvailableSpots</c> starts reporting a free seat that is
/// occupied — a number the customer's own DTO carries, and the input to
/// <c>AddAssignedEmployee</c>'s capacity guard.</para>
/// </summary>
public class CoverRequestSeatTests
{
    private const string OrderId = "order-cover-1";

    private static Order TwoSeatOrder() =>
        ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.Confirmed, maxEmployees: 2);

    private static int _seq;

    private static OrderEmployee Assign(Order order)
    {
        var assignment = OrderEmployee.Create(order, NewEmployee());
        order.AddAssignedEmployee(assignment);
        return assignment;
    }

    private static Cleansia.Core.Domain.Users.Employee NewEmployee() =>
        ValidatorTestHelpers.BuildEmployee($"emp-cover-{++_seq}", ContractStatus.Approved);

    [Fact]
    public void AnOrdinaryOrderHasNoCoverSeats()
    {
        var order = TwoSeatOrder();
        Assign(order);

        Assert.Equal(0, order.CoverSeatsOpen);
        Assert.True(order.HasAvailableSpots);
        Assert.True(order.HasTakeableSeat);
    }

    /// <summary>
    /// THE POINT OF THE WHOLE FEATURE. A full crew where somebody has asked for cover has NO available
    /// spot — they are still assigned — and yet the seat must be takeable, or nobody can relieve them.
    /// </summary>
    [Fact]
    public void AFullCrewSeekingCoverIsTakeableThoughNotAvailable()
    {
        var order = TwoSeatOrder();
        var first = Assign(order);
        Assign(order);

        first.MarkCoverRequested(DateTime.UtcNow);

        Assert.False(order.HasAvailableSpots);
        Assert.Equal(0, order.AvailableSpots);
        Assert.Equal(1, order.CoverSeatsOpen);
        Assert.True(order.HasTakeableSeat);
    }

    /// <summary>
    /// AvailableSpots is the CUSTOMER's number and the capacity guard's input. A cover request must not
    /// move it, or the platform starts believing an occupied seat is free.
    /// </summary>
    [Fact]
    public void ACoverRequestDoesNotCreateCapacity()
    {
        var order = TwoSeatOrder();
        var first = Assign(order);
        Assign(order);

        var before = order.AvailableSpots;
        first.MarkCoverRequested(DateTime.UtcNow);

        Assert.Equal(before, order.AvailableSpots);
        Assert.Throws<InvalidOperationException>(
            () => order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee())));
    }

    /// <summary>
    /// First stamp wins, which is why the command needs no "already requested" error key: a cleaner
    /// tapping twice on a bad connection gets a success, not a refusal, and the clock does not move.
    /// </summary>
    [Fact]
    public void ASecondRequestKeepsTheFirstStamp()
    {
        var order = TwoSeatOrder();
        var assignment = Assign(order);
        var first = DateTime.UtcNow.AddMinutes(-30);

        assignment.MarkCoverRequested(first);
        assignment.MarkCoverRequested(DateTime.UtcNow);

        Assert.Equal(first, assignment.CoverRequestedAt);
        Assert.Equal(1, order.CoverSeatsOpen);
    }

    /// <summary>
    /// The swap displaces the OLDEST request. Stated as an ordering rather than left to collection
    /// order, because two cleaners on one job can both be seeking cover and whoever asked first has
    /// been waiting longest.
    /// </summary>
    [Fact]
    public void TheOldestRequestIsTheOneDisplaced()
    {
        var order = TwoSeatOrder();
        var first = Assign(order);
        var second = Assign(order);

        second.MarkCoverRequested(DateTime.UtcNow.AddMinutes(-5));
        first.MarkCoverRequested(DateTime.UtcNow.AddMinutes(-60));

        var displaced = order.AssignedEmployees
            .Where(oe => oe.CoverRequestedAt is not null)
            .OrderBy(oe => oe.CoverRequestedAt)
            .ThenBy(oe => oe.SeatOrdinal)
            .First();

        Assert.Same(first, displaced);
        Assert.Equal(2, order.CoverSeatsOpen);
    }

    /// <summary>
    /// The swap is REMOVE-then-ADD, and the freed ordinal is reused. That is what makes two cleaners
    /// racing for one cover seat derive the SAME ordinal, so the unique index on
    /// (OrderId, SeatOrdinal) genuinely arbitrates. Add-first would give them different ordinals and
    /// the index would admit both.
    /// </summary>
    [Fact]
    public void TheReplacementReusesTheVacatedSeatOrdinal()
    {
        var order = TwoSeatOrder();
        var first = Assign(order);
        var second = Assign(order);
        var vacated = first.SeatOrdinal;

        first.MarkCoverRequested(DateTime.UtcNow);
        order.UnassignEmployee(first.EmployeeId);

        var replacement = OrderEmployee.Create(order, NewEmployee());
        order.AddAssignedEmployee(replacement);

        Assert.Equal(vacated, replacement.SeatOrdinal);
        Assert.NotEqual(second.SeatOrdinal, replacement.SeatOrdinal);
        Assert.Equal(0, order.CoverSeatsOpen);
        Assert.Equal(2, order.AssignedEmployees.Count);
    }
}
