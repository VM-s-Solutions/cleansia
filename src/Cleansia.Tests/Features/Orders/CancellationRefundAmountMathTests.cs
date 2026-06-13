using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// AC4 — the cancellation refund-amount formula <c>refundAmount = TotalPrice × (1 − feeRate)</c>,
/// pinned in pure <see cref="decimal"/> with independently HAND-DERIVED expected constants (never the
/// production expression re-run in the test). Each fee tier is sourced from the policy constant so a
/// change to a tier moves the rate but the arithmetic stays hand-checked. The no-float-drift cases use
/// values where a <c>double</c> intermediate would visibly disagree with the exact decimal.
/// </summary>
public class CancellationRefundAmountMathTests
{
    private static decimal Refund(decimal totalPrice, decimal feeRate) => totalPrice * (1m - feeRate);

    // Free cancellation → the whole price comes back.
    [Fact]
    public void FreeTier_RefundsFullTotalPrice()
    {
        Assert.Equal(2499m, Refund(2499m, 0m));
    }

    // 25% partial fee on 2400 → keep 1800.
    [Fact]
    public void PartialTier_Keeps75PercentOfTotalPrice()
    {
        // 2400 − 25% = 2400 × 0.75 = 1800 (hand-derived).
        Assert.Equal(1800m, Refund(2400m, BookingPolicy.PartialCancellationFeeRate));
    }

    // 50% last-minute fee on 2400 → keep 1200.
    [Fact]
    public void LastMinuteTier_KeepsHalfOfTotalPrice()
    {
        // 2400 × 0.50 = 1200 (hand-derived).
        Assert.Equal(1200m, Refund(2400m, BookingPolicy.LastMinuteCancellationFeeRate));
    }

    // Full fee (feeRate == 1) → nothing refunded.
    [Fact]
    public void FullFee_RefundsZero()
    {
        Assert.Equal(0m, Refund(2400m, 1m));
    }

    // No-float-drift: 0.1 + 0.2 is the canonical double-trap. 1999.99 × 0.75 is exactly 1499.9925 in
    // decimal; a double round-trip would not land on this constant.
    [Fact]
    public void DecimalMath_HasNoFloatingPointDrift_OnPartialTier()
    {
        // 1999.99 × (1 − 0.25) = 1999.99 × 0.75 = 1499.9925 (hand-derived, exact decimal).
        Assert.Equal(1499.9925m, Refund(1999.99m, 0.25m));
    }

    [Fact]
    public void DecimalMath_HasNoFloatingPointDrift_OnHalfFee()
    {
        // 333.33 × 0.5 = 166.665 (hand-derived, exact decimal — a double would round to 166.66499999…).
        Assert.Equal(166.665m, Refund(333.33m, 0.5m));
    }

    // Representative CZK amounts at each tier in one place (Theory), expected hand-derived.
    [Theory]
    [InlineData(1000, 0.00, 1000)]
    [InlineData(1000, 0.25, 750)]
    [InlineData(1000, 0.50, 500)]
    [InlineData(1500, 0.25, 1125)]
    [InlineData(1500, 0.50, 750)]
    [InlineData(890, 0.50, 445)]
    public void RepresentativeCzkAmounts_MatchHandDerivedRefund(decimal total, decimal feeRate, decimal expected)
    {
        Assert.Equal(expected, Refund(total, feeRate));
    }
}
