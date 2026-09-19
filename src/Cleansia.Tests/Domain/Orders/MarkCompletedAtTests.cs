using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// <see cref="Order.CompletedAt"/> is the axis the revenue report reads, and <see cref="Order.CompleteOrder"/>
/// is not the only way an order reaches <see cref="OrderStatus.Completed"/>: the administrator's override
/// writes the track without an actual duration to give <c>CompleteOrder</c>. <c>MarkCompletedAt</c> dates
/// that completion. First stamp wins, so a second call can never move an order into another month.
/// </summary>
public class MarkCompletedAtTests
{
    private static readonly DateTime March5 = new(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder() => Order.Create(
        customerName: "Stamp Customer",
        customerEmail: "stamp@cleansia.test",
        customerPhone: "+420000000000",
        customerAddress: Address.Create("Stamp St 1", "Prague", "11000", "cz"),
        rooms: 1,
        bathrooms: 1,
        cleaningDateTime: March5.AddHours(-2),
        paymentType: PaymentType.Card,
        totalPrice: 1000m,
        currencyId: "czk",
        paymentStatus: PaymentStatus.Paid,
        userId: "user-stamp");

    [Fact]
    public void Stamps_The_Given_Instant_On_An_Undated_Order()
    {
        var order = NewOrder();

        order.MarkCompletedAt(March5);

        Assert.Equal(March5, order.CompletedAt);
    }

    [Fact]
    public void The_First_Stamp_Wins()
    {
        var order = NewOrder();
        order.MarkCompletedAt(March5);

        order.MarkCompletedAt(March5.AddMonths(1));

        Assert.Equal(March5, order.CompletedAt);
    }

    [Fact]
    public void CompleteOrder_Still_Stamps_Now_On_An_Undated_Order()
    {
        var order = NewOrder();
        var before = DateTime.UtcNow;

        order.CompleteOrder(90);

        Assert.NotNull(order.CompletedAt);
        Assert.InRange(order.CompletedAt!.Value, before, DateTime.UtcNow);
    }
}
