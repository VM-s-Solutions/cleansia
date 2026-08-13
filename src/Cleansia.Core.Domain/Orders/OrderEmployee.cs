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

    public static OrderEmployee Create(Order order, Employee employee) => new()
    {
        Order = order,
        OrderId = order.Id,
        Employee = employee,
        EmployeeId = employee.Id
    };

    internal void AssignSeatOrdinal(int seatOrdinal) => SeatOrdinal = seatOrdinal;
}