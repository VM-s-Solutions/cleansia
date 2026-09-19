using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// The one writer of <c>Confirmed → New</c>. <c>Confirmed</c> says a cleaner took the job; when a release
/// leaves nobody on it the word is false and the order goes back on the board. The method decides on
/// two facts it can read — the crew count and the current status — and nothing else: an order past
/// <c>Confirmed</c> is a cleaner in a home, never walked back by the platform, and a crew that remains
/// means the job is still staffed.
/// </summary>
public class ReturnToBoardIfUnstaffedTests
{
    // In the past, so the rows a walk-back appends (stamped at construction) are newer than the fixture's.
    private static readonly DateTime Now = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Order OrderAt(params OrderStatus[] history)
    {
        var order = Order.Create(
            customerName: "Board Customer",
            customerEmail: "board@cleansia.test",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Board St 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: Now.AddHours(24),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "user-board");
        order.SetMaxEmployees(2);
        var stamp = new DateTimeOffset(Now).AddMinutes(-history.Length);
        foreach (var status in history)
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("test", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }

    private static Employee Cleaner(string id)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Passw0rd!x", "Board", "Cleaner", UserProfile.Employee);
        user.Id = $"{id}-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = id;
        return employee;
    }

    [Fact]
    public void A_Confirmed_Order_With_Nobody_On_It_Goes_Back_To_New_With_The_Next_Sequence()
    {
        var order = OrderAt(OrderStatus.New, OrderStatus.Confirmed);
        var rowsBefore = order.OrderStatusHistory.Count;
        var lastSequence = order.OrderStatusHistory.Max(s => s.Sequence);

        var walkedBack = order.ReturnToBoardIfUnstaffed();

        Assert.True(walkedBack);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(rowsBefore + 1, order.OrderStatusHistory.Count);
        var appended = order.OrderStatusHistory.OrderByDescending(s => s.Sequence).First();
        Assert.Equal(OrderStatus.New, appended.Status);
        Assert.Equal(lastSequence + 1, appended.Sequence);
    }

    [Fact]
    public void A_Confirmed_Order_With_A_Crew_Is_Left_Alone()
    {
        var order = OrderAt(OrderStatus.New, OrderStatus.Confirmed);
        order.AddAssignedEmployee(OrderEmployee.Create(order, Cleaner("emp-1")));
        var rowsBefore = order.OrderStatusHistory.Count;

        var walkedBack = order.ReturnToBoardIfUnstaffed();

        Assert.False(walkedBack);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(rowsBefore, order.OrderStatusHistory.Count);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void An_Order_That_Is_Not_Confirmed_Is_Never_Walked_Back(OrderStatus status)
    {
        var order = OrderAt(OrderStatus.New, status);
        var rowsBefore = order.OrderStatusHistory.Count;

        var walkedBack = order.ReturnToBoardIfUnstaffed();

        Assert.False(walkedBack);
        Assert.Equal(status, order.CurrentStatus);
        Assert.Equal(rowsBefore, order.OrderStatusHistory.Count);
    }

    /// <summary>
    /// <c>CurrentStatus</c> is recomputed from the history, not set to the appended value — the same rule
    /// every other status write follows, so a walk-back and a later row never disagree about which one
    /// is current.
    /// </summary>
    [Fact]
    public void The_Walked_Back_Order_Can_Be_Taken_Again_And_Reads_Confirmed()
    {
        var order = OrderAt(OrderStatus.New, OrderStatus.Confirmed);
        order.ReturnToBoardIfUnstaffed();

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal([0, 1, 2, 3], order.OrderStatusHistory.Select(s => s.Sequence).OrderBy(s => s));
    }
}
