using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Orders;

public class OrderEmployee : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }

    public static OrderEmployee Create(Order order, Employee employee) => new()
    {
        Order = order,
        OrderId = order.Id,
        Employee = employee,
        EmployeeId = employee.Id
    };
}