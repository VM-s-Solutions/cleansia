using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Orders;
using Cleansia.TestUtilities.MockDataFactories.Orders;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// The two pure pieces of applying credit at checkout: how much may be spent, and what the card is
/// then asked for.
///
/// <para>The debit itself is not here — it is a Postgres CTE, and
/// <c>Cleansia.IntegrationTests.Features.Credit.CreditDebitTests</c> is the only place its semantics
/// are real.</para>
/// </summary>
public class CreditAtCheckoutTests
{
    [Theory]
    // Balance well under the ceiling: all of it is spendable.
    [InlineData(500, 2000, 500)]
    // Balance above the ceiling: capped at 80% of the order, and the card pays the other 20%.
    [InlineData(2000, 1000, 800)]
    // Exactly at the ceiling.
    [InlineData(800, 1000, 800)]
    // No balance, no order, and negative nonsense all answer zero rather than throwing — this runs on
    // every card checkout, and it must be uneventful.
    [InlineData(0, 1000, 0)]
    [InlineData(500, 0, 0)]
    [InlineData(-100, 1000, 0)]
    public void CapCreditForOrder_LeavesTheCardAShare(
        decimal balance, decimal totalPrice, decimal expected)
    {
        Assert.Equal(expected, BookingPolicy.CapCreditForOrder(balance, totalPrice));
    }

    /// <summary>
    /// A customer can NEVER settle a whole order from credit — owner ruling 2026-09-05. Asserted as a
    /// property over a spread of totals rather than one example, because the failure mode is a
    /// rounding edge at one price, not a wrong constant.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99.99)]
    [InlineData(1000)]
    [InlineData(13333.33)]
    public void CapCreditForOrder_NeverCoversTheWholeOrder(decimal totalPrice)
    {
        var applied = BookingPolicy.CapCreditForOrder(balance: 1_000_000m, totalPrice: totalPrice);

        Assert.True(applied < totalPrice,
            $"{applied} of credit would have settled all of a {totalPrice} order");
    }

    /// <summary>
    /// Rounded DOWN to whole minor units. Stripe takes integer cents, so a credit carrying a third of
    /// a cent would be absorbed by whichever side rounded — and the two sides round differently.
    /// </summary>
    [Fact]
    public void CapCreditForOrder_IsWholeMinorUnits()
    {
        // 80% of 33.33 is 26.664.
        var applied = BookingPolicy.CapCreditForOrder(balance: 1000m, totalPrice: 33.33m);

        Assert.Equal(26.66m, applied);
    }

    /// <summary>
    /// THE ONE THAT MATTERS: credit is a TENDER. The sale keeps its size — the fiscal receipt and
    /// loyalty both read <c>TotalPrice</c> — and only the figure the card is asked for moves.
    /// </summary>
    [Fact]
    public void ApplyCredit_MovesTheCardAmountAndNothingElse()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = "01ORDERCREDIT0000000000001",
            TotalPrice = 2000m,
        });

        var netBefore = order.NetAmount;
        var vatBefore = order.VatAmount;

        order.ApplyCredit(500m, "actor");

        Assert.Equal(2000m, order.TotalPrice);
        Assert.Equal(netBefore, order.NetAmount);
        Assert.Equal(vatBefore, order.VatAmount);
        Assert.Equal(1500m, order.AmountDueOnCard);
    }

    [Fact]
    public void AmountDueOnCard_IsTheFullPriceWhenNoCreditWasApplied()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = "01ORDERCREDIT0000000000002",
            TotalPrice = 1234.50m,
        });

        Assert.Equal(0m, order.CreditAppliedAmount);
        Assert.Equal(order.TotalPrice, order.AmountDueOnCard);
    }

    /// <summary>
    /// The domain refuses a credit larger than the sale outright. The cap should make this
    /// unreachable, so this pins the guard beneath it rather than the rule above it.
    /// </summary>
    [Fact]
    public void ApplyCredit_RefusesMoreThanTheOrderIsWorth()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = "01ORDERCREDIT0000000000003",
            TotalPrice = 100m,
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => order.ApplyCredit(101m, "actor"));
    }
}
