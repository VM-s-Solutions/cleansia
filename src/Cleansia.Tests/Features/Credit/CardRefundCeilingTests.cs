using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// The most that can still go back to the CARD once part of an order was settled from the customer's
/// credit balance.
///
/// <para><b>The hole this closes.</b> Credit is a tender, so a 2000 order paid with 500 of credit and
/// 1500 of card is still a 2000 sale — but only 1500 ever reached Stripe. A ceiling of
/// <c>TotalPrice − consumed</c> would let that customer be refunded the whole 2000 in cash, turning
/// credit into money at will and taking 500 out of the company on every round trip.</para>
///
/// <para>The credit half comes back by an admin re-issuing it, not automatically — human-in-the-loop
/// is the standing ruling, and it keeps this a subtraction rather than a second money path.</para>
/// </summary>
public class CardRefundCeilingTests
{
    private static Order OrderOf(decimal totalPrice, decimal creditApplied)
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");

        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, "system");
        }

        return order;
    }

    [Fact]
    public void WithNoCreditApplied_TheCeilingIsUnchanged()
    {
        // Every order written before credit existed carries zero, so nothing shifts for them.
        Assert.Equal(2000m, RefundService.CardRefundCeiling(OrderOf(2000m, 0m), consumed: 0m));
    }

    [Fact]
    public void CreditIsSubtracted_BecauseTheCardNeverTookIt()
    {
        Assert.Equal(1500m, RefundService.CardRefundCeiling(OrderOf(2000m, 500m), consumed: 0m));
    }

    [Fact]
    public void EarlierRefundsAreSubtractedToo()
    {
        Assert.Equal(1100m, RefundService.CardRefundCeiling(OrderOf(2000m, 500m), consumed: 400m));
    }

    [Fact]
    public void AFullyCreditedOrder_HasNothingToRefundToTheCard()
    {
        // Not an error — there is simply no card leg. The credit is returned by re-issuing it.
        Assert.Equal(0m, RefundService.CardRefundCeiling(OrderOf(2000m, 2000m), consumed: 0m));
    }

    [Fact]
    public void TheCeilingCanGoNegative_AndTheCallerTreatsThatAsNothingRefundable()
    {
        // Both call sites already refuse on <= 0; this pins that the arithmetic does not silently
        // floor and hand back a positive figure it should not.
        Assert.True(RefundService.CardRefundCeiling(OrderOf(2000m, 500m), consumed: 1600m) < 0m);
    }
}
