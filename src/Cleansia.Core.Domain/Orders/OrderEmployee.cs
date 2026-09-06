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
    /// cleaner and silently skip the second. <c>CompleteOrder</c> carried a comment asserting the
    /// opposite until 2026-08-22, which is how long the belief survived unexamined.</para>
    ///
    /// <para>An unassign hard-DELETEs this row, so a reassignment correctly reminds the replacement
    /// cleaner with a fresh null stamp rather than inheriting the previous one's receipt.</para>
    /// </summary>
    public DateTime? ReminderSoonSentAt { get; private set; }

    /// <summary>
    /// When this cleaner was nudged that the job starts shortly and they had not set off. Null until
    /// the sweep fires; never cleared. Distinct from <see cref="ReminderSoonSentAt"/> in both timing and
    /// precondition — that one is unconditional at T-2h, this one is suppressed when the cleaner is
    /// already out on another job. Neither is gated on THIS assignment's own progress: the sweep selects
    /// only <c>Confirmed</c> orders, so both stop once the order itself moves.
    /// </summary>
    public DateTime? ReminderNotStartedSentAt { get; private set; }

    /// <summary>
    /// When this cleaner asked for someone to take the job off them. Null on an ordinary assignment.
    ///
    /// <para><b>The cleaner stays assigned while this is set</b> — owner ruling 2026-09-06. That is the
    /// whole point of a cover request rather than a drop: the seat is still filled, the job is still
    /// deliverable if nobody answers, and the customer's reminders still fire. What changes is that the
    /// seat becomes TAKEABLE by another cleaner, who displaces this one on arrival.</para>
    ///
    /// <para>On the assignment rather than the Order for the same reason the two reminder stamps are:
    /// a crew is <c>ceil(EstimatedTime / 120)</c> and two-seat orders exist today, so a scalar on
    /// <c>Order</c> could not say WHICH seat is seeking cover.</para>
    ///
    /// <para>Not indexed. The satisfying set is NULL-dominant and every query that reads it has already
    /// been narrowed by the order's own status and date — a partial index on the non-null rows would
    /// index precisely what the predicate excludes. Same argument the file's siblings make.</para>
    /// </summary>
    public DateTime? CoverRequestedAt { get; private set; }

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

    /// <summary>
    /// First stamp wins, so a double tap is a no-op success rather than a moved deadline — which is
    /// why the command needs no "already requested" error key.
    /// </summary>
    public OrderEmployee MarkCoverRequested(DateTime requestedAtUtc)
    {
        CoverRequestedAt ??= requestedAtUtc;
        return this;
    }

    /// <summary>The cleaner changed their mind and is keeping the job.</summary>
    public OrderEmployee ClearCoverRequest()
    {
        CoverRequestedAt = null;
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