using Cleansia.Core.AppServices.Features.Refunds;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// ADR-0009 D2 — the share-of-<c>TotalPrice</c> allocator. The frozen <c>Order.TotalPrice</c> already
/// embeds discount + express; the allocator only splits it across the selected lines and never
/// re-applies any modifier. Penny-perfect: the last selected line absorbs the sub-cent residual.
/// </summary>
public class RefundAllocatorTests
{
    private static IReadOnlyList<RefundAllocationLine> Lines(params (decimal gross, bool selected)[] rows) =>
        rows.Select(r => new RefundAllocationLine(r.gross, r.selected)).ToList();

    // TC-REFUND-ALLOC
    [Fact]
    public void FullMultiLineRefund_OnDiscountedExpressOrder_ReconcilesPennyPerfectToTotalPrice()
    {
        // List grosses sum to 1000; the booked TotalPrice is 850 (a discount + express already baked in).
        // A full refund of all three lines must sum EXACTLY to 850, never to the 1000 list total — proving
        // discount/express are not re-applied and the frozen total is what gets split.
        var allocation = RefundAllocator.Allocate(
            Lines((300m, true), (300m, true), (400m, true)),
            totalPrice: 850m,
            appliedVatRate: null);

        var amounts = allocation.Select(a => a.RefundAmount).ToList();
        Assert.Equal(850m, amounts.Sum());
        Assert.All(amounts, a => Assert.True(a > 0m));
    }

    // TC-REFUND-ALLOC — last selected line absorbs the sub-cent residual.
    [Fact]
    public void LastSelectedLine_AbsorbsTheSubCentResidual()
    {
        // 3 equal lines, TotalPrice 10 → 3.33 + 3.33 + 3.34 with the last absorbing the cent.
        var allocation = RefundAllocator.Allocate(
            Lines((1m, true), (1m, true), (1m, true)),
            totalPrice: 10m,
            appliedVatRate: null);

        Assert.Equal(3.33m, allocation[0].RefundAmount);
        Assert.Equal(3.33m, allocation[1].RefundAmount);
        Assert.Equal(3.34m, allocation[2].RefundAmount);
        Assert.Equal(10m, allocation.Sum(a => a.RefundAmount));
    }

    // TC-REFUND-ALLOC — partial selection reconciles to its own rounded share, not to TotalPrice.
    [Fact]
    public void PartialSelection_ReconcilesToTheSelectionsRoundedShareOfTotalPrice()
    {
        // Select 2 of 3 equal lines. Their share of TotalPrice 100 is round(2/3 * 100) = 66.67.
        var allocation = RefundAllocator.Allocate(
            Lines((1m, true), (1m, true), (1m, false)),
            totalPrice: 100m,
            appliedVatRate: null);

        var selected = allocation.Where(a => a.Selected).ToList();
        Assert.Equal(66.67m, selected.Sum(a => a.RefundAmount));
        Assert.Equal(0m, allocation[2].RefundAmount);
    }

    // TC-REFUND-VAT
    [Fact]
    public void Vat_IsApportionedPerLine_AtTheVatFractionOfGross()
    {
        // 21% VAT: refundVat = round(lineRefund * 21 / 121, 2).
        var allocation = RefundAllocator.Allocate(
            Lines((1m, true)),
            totalPrice: 121m,
            appliedVatRate: 21m);

        Assert.Equal(121m, allocation[0].RefundAmount);
        Assert.Equal(21m, allocation[0].RefundVat);
    }

    // TC-REFUND-VAT — null AppliedVatRate (non-VAT-payer) yields zero apportioned VAT.
    [Fact]
    public void Vat_IsZero_WhenAppliedVatRateIsNull()
    {
        var allocation = RefundAllocator.Allocate(
            Lines((1m, true), (1m, true)),
            totalPrice: 200m,
            appliedVatRate: null);

        Assert.All(allocation, a => Assert.Equal(0m, a.RefundVat));
    }

    [Fact]
    public void NoSelection_YieldsAllZeroAmounts()
    {
        var allocation = RefundAllocator.Allocate(
            Lines((1m, false), (1m, false)),
            totalPrice: 100m,
            appliedVatRate: null);

        Assert.All(allocation, a => Assert.Equal(0m, a.RefundAmount));
    }

    [Fact]
    public void NonPositiveTotalGross_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RefundAllocator.Allocate(Lines((0m, true)), 100m, null));
    }
}
