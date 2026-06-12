using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// AC1 (AUD-15) — cancellation is attributed to a typed actor. <c>Order.Cancel(...)</c> takes a
/// <see cref="CancelledBy"/> enum (no longer a free-text string), and every actor round-trips through
/// the entity so the persisted attribution can never be a typo or an unknown role.
/// </summary>
public class OrderCancelActorTests
{
    private static Order ArrangeOrder()
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
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.SetCurrency(currency);
        return order;
    }

    [Theory]
    [InlineData(CancelledBy.Customer)]
    [InlineData(CancelledBy.Cleaner)]
    [InlineData(CancelledBy.Admin)]
    [InlineData(CancelledBy.System)]
    public void Cancel_RecordsTheActor_RoundTrips(CancelledBy actor)
    {
        var order = ArrangeOrder();
        var now = DateTime.UtcNow;

        order.Cancel(
            cancelledAtUtc: now,
            cancelledBy: actor,
            feeRate: 0m,
            refundAmount: 1000m,
            reason: "test");

        Assert.Equal(actor, order.CancelledBy);
        Assert.Equal(now, order.CancelledAt);
        Assert.Equal(0m, order.CancellationFeeRate);
        Assert.Equal(1000m, order.CancellationRefundAmount);
        Assert.Equal("test", order.CancellationReason);
    }
}
