namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>
/// One selectable refund line and the per-line outcome of the share-of-<c>TotalPrice</c> split.
/// </summary>
public readonly record struct RefundAllocationLine(decimal Gross, bool Selected);

public readonly record struct RefundAllocationResult(decimal RefundAmount, decimal RefundVat, bool Selected);

/// <summary>
/// ADR-0009 D2 — apportions the FROZEN <c>Order.TotalPrice</c> across the selected lines by each line's
/// share of the gross. <c>TotalPrice</c> already has discount + express + loyalty baked in, so this
/// never re-applies them: it only splits the one frozen number. Sibling in style to
/// <c>PackagePricing.DeriveIncludedServiceGrosses</c> (the last selected line absorbs the sub-cent
/// residual, so the selection reconciles penny-perfect to its exact rounded target).
/// </summary>
public static class RefundAllocator
{
    /// <summary>
    /// For each line returns its refund amount + apportioned VAT.
    /// <para>
    /// Per-line refund is <c>round(lineGross / Σ(allGrosses) × TotalPrice, 2)</c> (ADR-0009 D2): the
    /// denominator is EVERY order line, so a bundled service's slice can never exceed its package line's
    /// share of <c>TotalPrice</c>. The last SELECTED line absorbs the rounding residual so the selected
    /// amounts sum exactly to the selection's rounded target <c>round(Σ(selectedGross)/Σ(allGrosses) ×
    /// TotalPrice, 2)</c> — equal to <c>TotalPrice</c> when the whole order is selected.
    /// </para>
    /// VAT per line is <c>round(lineRefund × rate / (100 + rate), 2)</c>, and 0 when
    /// <paramref name="appliedVatRate"/> is null (non-VAT-payer order).
    /// </summary>
    public static IReadOnlyList<RefundAllocationResult> Allocate(
        IReadOnlyList<RefundAllocationLine> lines,
        decimal totalPrice,
        decimal? appliedVatRate)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var totalGross = lines.Sum(l => l.Gross);
        if (totalGross <= 0m)
        {
            throw new ArgumentException("The sum of line grosses must be positive.", nameof(lines));
        }

        var selectedIndices = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Selected)
            {
                selectedIndices.Add(i);
            }
        }

        var results = new RefundAllocationResult[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            results[i] = new RefundAllocationResult(0m, 0m, lines[i].Selected);
        }

        if (selectedIndices.Count == 0)
        {
            return results;
        }

        var selectedGross = selectedIndices.Sum(i => lines[i].Gross);
        var selectionTarget = Math.Round(selectedGross / totalGross * totalPrice, 2, MidpointRounding.AwayFromZero);

        decimal allocated = 0m;
        for (var k = 0; k < selectedIndices.Count - 1; k++)
        {
            var index = selectedIndices[k];
            var amount = Math.Round(lines[index].Gross / totalGross * totalPrice, 2, MidpointRounding.AwayFromZero);
            allocated += amount;
            results[index] = new RefundAllocationResult(amount, ApportionVat(amount, appliedVatRate), Selected: true);
        }

        var lastIndex = selectedIndices[^1];
        var lastAmount = selectionTarget - allocated;
        results[lastIndex] = new RefundAllocationResult(lastAmount, ApportionVat(lastAmount, appliedVatRate), Selected: true);

        return results;
    }

    private static decimal ApportionVat(decimal lineRefund, decimal? appliedVatRate)
    {
        if (appliedVatRate is not { } rate || rate <= 0m)
        {
            return 0m;
        }

        return Math.Round(lineRefund * rate / (100m + rate), 2, MidpointRounding.AwayFromZero);
    }
}
