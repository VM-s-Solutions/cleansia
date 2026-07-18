using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The shared assignment-cancellation notifier (T-0431): every cleaner who ACCEPTED a job hears
/// when it's cancelled — a gap today, since the cancel handlers only ever notified the customer. A
/// dedicated partner event (not the customer <c>order.cancelled</c>), one feed row + push per
/// assigned cleaner, skipping legacy assignments with no linked user.
/// </summary>
public class OrderAssignmentCancellationNotifierTests
{
    private const string OrderId = "ord-assign-cancel-1";
    private const string OrderNumber = "A-2201";

    private readonly Mock<INotificationProducer> _producer = new();

    private static Order NewOrder()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "customer-1");
        order.Id = OrderId;
        order.SetCurrency(currency);
        // DisplayOrderNumber has a private setter (auto-generated at construction); invoke it via
        // reflection so the notify args are deterministic.
        typeof(Order).GetProperty(nameof(Order.DisplayOrderNumber))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(order, [OrderNumber]);
        // Default MaxEmployees is 1; let the fixtures assign a small crew.
        typeof(Order).GetProperty(nameof(Order.MaxEmployees))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(order, [5]);
        return order;
    }

    private static Employee EmployeeWith(string userId)
    {
        var user = User.CreateWithPassword($"{userId}@cleansia.test", "Passw0rd!", "Clean", "Er");
        user.Id = userId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = $"emp-{userId}";
        return employee;
    }

    private void Assign(Order order, params Employee[] employees)
    {
        foreach (var e in employees)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(order, e));
        }
    }

    [Fact]
    public async Task Notifies_Each_Assigned_Employee_With_The_Partner_Event_And_Order_Args()
    {
        var order = NewOrder();
        Assign(order, EmployeeWith("emp-user-a"), EmployeeWith("emp-user-b"));

        await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
            order, _producer.Object, CancellationToken.None);

        foreach (var userId in new[] { "emp-user-a", "emp-user-b" })
        {
            _producer.Verify(p => p.NotifyAsync(
                userId,
                NotificationEventCatalog.OrderAssignmentCancelled,
                It.Is<Dictionary<string, string>>(d =>
                    d["orderId"] == OrderId && d["orderNumber"] == OrderNumber),
                order.TenantId,
                OrderId,
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
        _producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_Unassigned_Order_Notifies_No_One()
    {
        var order = NewOrder();

        await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
            order, _producer.Object, CancellationToken.None);

        _producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Skips_An_Assignment_Whose_Employee_Has_No_Linked_User()
    {
        var order = NewOrder();
        Assign(order, EmployeeWith("real-user"), EmployeeWith(string.Empty));

        await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
            order, _producer.Object, CancellationToken.None);

        _producer.Verify(p => p.NotifyAsync(
            "real-user",
            NotificationEventCatalog.OrderAssignmentCancelled,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _producer.VerifyNoOtherCalls();
    }
}
