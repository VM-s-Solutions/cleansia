using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Test fixture builders for Order/Employee/Address that satisfy the
/// minimum shape the order-flow validators query against:
/// <c>Id</c> + <c>OrderStatusHistory</c> (latest by <c>CreatedOn</c>) +
/// <c>AssignedEmployees</c>.
///
/// Bypasses the full <see cref="Order.Create"/> factory because the
/// validator only reads three navigation properties — constructing the
/// remaining 12+ Required fields is dead weight here.
/// </summary>
internal static class ValidatorTestHelpers
{
    public static Order BuildOrder(
        string orderId,
        OrderStatus currentStatus,
        string assignedEmployeeId,
        PaymentType paymentType = PaymentType.Cash,
        PaymentStatus paymentStatus = PaymentStatus.Pending)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus);

        order.Id = orderId;

        // OrderStatusHistory: append in chronological order. The validator
        // queries by max CreatedOn, so a single entry suffices but we add
        // a "New" then the target status so the OrderByDescending path is
        // exercised. CreatedOn is set via Auditable.Created(by, on) so the
        // ordering is deterministic in-memory.
        var now = DateTimeOffset.UtcNow;
        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", now.AddMinutes(-10));
        order.AddOrderStatus(initial);

        if (currentStatus != OrderStatus.New)
        {
            var latest = OrderStatusTrack.Create(currentStatus, order);
            latest.Created("test", now);
            order.AddOrderStatus(latest);
        }

        var user = User.CreateWithPassword("emp@example.com", "x", "Emp", "Loyee");
        user.Id = assignedEmployeeId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = assignedEmployeeId;
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));

        return order;
    }

    /// <summary>
    /// An order with the given status history but NO assigned employees and a
    /// raised <c>MaxEmployees</c> so it has an open spot. Used by the TakeOrder
    /// gate tests, where the caller must not already be assigned.
    /// </summary>
    public static Order BuildEmptyOrder(string orderId, OrderStatus currentStatus, int maxEmployees = 2)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);

        order.Id = orderId;
        order.SetMaxEmployees(maxEmployees);

        var now = DateTimeOffset.UtcNow;
        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", now.AddMinutes(-10));
        order.AddOrderStatus(initial);

        if (currentStatus != OrderStatus.New)
        {
            var latest = OrderStatusTrack.Create(currentStatus, order);
            latest.Created("test", now);
            order.AddOrderStatus(latest);
        }

        return order;
    }

    /// <summary>
    /// A standalone employee with the given <see cref="ContractStatus"/> for the
    /// order-action approval gate tests. <paramref name="withAddress"/> controls
    /// the separate profile-complete rule so the approval gate can be tested in
    /// isolation from the address/profile gate.
    /// </summary>
    public static Employee BuildEmployee(string employeeId, ContractStatus contractStatus, bool withAddress = true)
    {
        var user = User.CreateWithPassword("emp@example.com", "x", "Emp", "Loyee");
        user.Id = employeeId + "-user";

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.UpdateContractStatus(contractStatus);

        if (withAddress)
        {
            employee.UpdateAddress(Address.Create("123 Main St", "Prague", "11000", "cz"));
        }

        return employee;
    }
}
