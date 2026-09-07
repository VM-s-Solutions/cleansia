using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// How a refund divides across the two tenders that settled an order.
///
/// <para>Credit is spent by one path and given back by six, so the arithmetic that decides how much
/// goes to the card and how much to the balance is the piece worth pinning hardest. It is a pure
/// function on the order, exactly like <c>RefundService.CardRefundCeiling</c>, and testing it by
/// calling it is the only way to test it that cannot silently drift from the real one.</para>
/// </summary>
public class CreditRefundSplitTests
{
    private static Order Order(decimal totalPrice, decimal creditApplied)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Cleansia.Core.Domain.Orders.Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.SetCurrency(currency);
        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, "actor");
        }
        return order;
    }

    /// <summary>
    /// THE RULE: proportional. A 2000 order settled with 500 credit and 1500 card, unwound by half,
    /// gives 750 back to the card and 250 back to the balance — the customer has paid 1000, in the
    /// same mix they paid it in.
    /// </summary>
    [Fact]
    public void AHalfRefund_GivesBackHalfOfEachTender()
    {
        var (card, credit) = RefundService.SplitAcrossTenders(Order(2000m, 500m), 1000m);

        Assert.Equal(750m, card);
        Assert.Equal(250m, credit);
    }

    [Fact]
    public void AFullRefund_GivesBackEveryTenderInFull()
    {
        var (card, credit) = RefundService.SplitAcrossTenders(Order(2000m, 500m), 2000m);

        Assert.Equal(1500m, card);
        Assert.Equal(500m, credit);
    }

    /// <summary>
    /// THE ONE THAT KEEPS EVERY SHIPPED REFUND BYTE-IDENTICAL: an order that took no credit splits to
    /// (requested, 0). This function now sits in front of every refund the platform issues, and it has
    /// to be the identity on the overwhelming majority of them.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(1999.99)]
    [InlineData(2000)]
    public void AnOrderThatTookNoCredit_SplitsToTheRequestAndNothing(decimal requested)
    {
        var (card, credit) = RefundService.SplitAcrossTenders(Order(2000m, 0m), requested);

        Assert.Equal(requested, card);
        Assert.Equal(0m, credit);
    }

    /// <summary>
    /// A 100% cancellation fee refunds nothing, and that includes the credit leg. The customer
    /// forfeits what they paid, in whatever they paid it.
    /// </summary>
    [Fact]
    public void ARefundOfNothing_GivesBackNothing()
    {
        var (card, credit) = RefundService.SplitAcrossTenders(Order(2000m, 500m), 0m);

        Assert.Equal(0m, card);
        Assert.Equal(0m, credit);
    }

    /// <summary>
    /// The two legs must sum to EXACTLY the requested amount at every price, or the difference is
    /// money that silently disappears on one side or the other. The card takes the rounding remainder,
    /// because Stripe is the leg that cannot accept a fraction of a cent.
    /// </summary>
    [Theory]
    [InlineData(999.99, 333.33, 111.11)]
    [InlineData(1000, 333.33, 777.77)]
    [InlineData(33.33, 26.66, 11.11)]
    [InlineData(7, 5.6, 3)]
    [InlineData(2000, 1, 1999)]
    public void TheTwoLegsAlwaysSumToTheRequest(decimal total, decimal creditApplied, decimal requested)
    {
        var (card, credit) = RefundService.SplitAcrossTenders(Order(total, creditApplied), requested);

        Assert.Equal(requested, card + credit);
        Assert.Equal(credit, decimal.Round(credit, 2));
    }

    /// <summary>
    /// Never give back more credit than was taken. The cap makes this unreachable through the normal
    /// path, so this pins the floor beneath it.
    /// </summary>
    [Fact]
    public void TheCreditLegNeverExceedsWhatWasApplied()
    {
        var (_, credit) = RefundService.SplitAcrossTenders(Order(1000m, 800m), 5000m);

        Assert.True(credit <= 800m, $"{credit} returned against 800 applied");
    }

    /// <summary>
    /// The card ceiling and the split have to agree, or the service clamps away part of a leg it just
    /// computed. On any slice of any credit-bearing order the card leg must fit under the ceiling.
    /// </summary>
    [Theory]
    [InlineData(2000, 500, 2000)]
    [InlineData(2000, 500, 1000)]
    [InlineData(999.99, 333.33, 999.99)]
    [InlineData(33.33, 26.66, 33.33)]
    public void TheCardLegAlwaysFitsUnderTheCardCeiling(
        decimal total, decimal creditApplied, decimal requested)
    {
        var order = Order(total, creditApplied);

        var (card, _) = RefundService.SplitAcrossTenders(order, requested);
        var ceiling = RefundService.CardRefundCeiling(order, consumed: 0m);

        Assert.True(card <= ceiling, $"card leg {card} exceeds ceiling {ceiling}");
    }
}
