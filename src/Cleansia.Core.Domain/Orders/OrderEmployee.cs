using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Orders;

public class OrderEmployee : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }

    // Which seat of the order this assignment occupies. Unique per order at the database
    // (IX_OrderEmployees_OrderId_SeatOrdinal), which is what actually stops two cleaners taking the same
    // seat — the in-memory capacity checks are three unlocked reads and cannot. Assigned in
    // Order.AddAssignedEmployee, never by the caller, exactly like OrderStatusTrack.Sequence.
    public int SeatOrdinal { get; private set; }

    /// <summary>
    /// When this cleaner was told their job starts in about two hours. Null until the sweep fires;
    /// never cleared.
    ///
    /// <para><b>On the assignment, not on the Order.</b> An order's crew is
    /// <c>ceil(EstimatedTime / 120)</c> and the seeded catalogue contains single 180- and 240-minute
    /// services, so two-seat orders exist today — a scalar on <c>Order</c> would remind the first
    /// cleaner and silently skip the second. Two in-tree comments claim crews are always size 1; both
    /// are wrong.</para>
    ///
    /// <para>An unassign hard-DELETEs this row, so a reassignment correctly reminds the replacement
    /// cleaner with a fresh null stamp rather than inheriting the previous one's receipt.</para>
    /// </summary>
    public DateTime? ReminderSoonSentAt { get; private set; }

    /// <summary>
    /// When this cleaner was nudged that the job starts shortly and they had not set off. Null until
    /// the sweep fires; never cleared. Distinct from <see cref="ReminderSoonSentAt"/> in both timing
    /// and precondition — that one is unconditional at T-2h, this one fires only while the assignment
    /// is still short of <c>OnTheWay</c>.
    /// </summary>
    public DateTime? ReminderNotStartedSentAt { get; private set; }

    /// <summary>First stamp wins, so a re-entrant sweep cannot move it forward and re-open a second send.</summary>
    public OrderEmployee MarkReminderSoonSent(DateTime sentAtUtc)
    {
        ReminderSoonSentAt ??= sentAtUtc;
        return this;
    }

    /// <inheritdoc cref="MarkReminderSoonSent"/>
    public OrderEmployee MarkReminderNotStartedSent(DateTime sentAtUtc)
    {
        ReminderNotStartedSentAt ??= sentAtUtc;
        return this;
    }

    public static OrderEmployee Create(Order order, Employee employee) => new()
    {
        Order = order,
        OrderId = order.Id,
        Employee = employee,
        EmployeeId = employee.Id
    };

    internal void AssignSeatOrdinal(int seatOrdinal) => SeatOrdinal = seatOrdinal;
}