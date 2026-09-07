using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The money block on a customer's receipt, asserted on the label/value pairs it composes.
///
/// <para>An order settled partly from a customer's credit balance charges the card less than the
/// receipt's Total, and the receipt said nothing about it — a customer holding a 2000 Kč receipt
/// against a 1500 Kč card statement had no way to reconcile the two, and the obvious conclusion is
/// that they were overcharged.</para>
///
/// <para><b>Total does not move, and that is the point.</b> Credit is a tender, not a discount: the
/// sale keeps its size, the VAT breakdown above it is the taxable base the fiscal authority already
/// registered, and shrinking either to explain a smaller card charge would register a 2000 clean with
/// the authority as 1500. The two tender lines answer the customer's question without touching the
/// fiscal one.</para>
/// </summary>
public class ReceiptTenderLayoutTests
{
    private static readonly ProbeLayout Layout = new();

    private static ReceiptPdfData Receipt(
        decimal total = 2000m, decimal creditApplied = 0m, bool vatPayer = true) =>
        new()
        {
            ReceiptNumber = "R-1",
            OrderNumber = "O-1",
            IssuedDate = "05.09.2026",
            CustomerName = "Jan Novák",
            Services = [],
            Packages = [],
            Total = total,
            CreditApplied = creditApplied,
            AmountDueOnCard = total - creditApplied,
            Currency = "Kč",
            PaymentStatus = "Paid",
            IsVatPayer = vatPayer,
            NetAmount = vatPayer ? total / 1.21m : null,
            VatAmount = vatPayer ? total - (total / 1.21m) : null,
            VatRate = vatPayer ? 21m : null,
        };

    /// <summary>
    /// The overwhelming majority of receipts. Two extra rows saying "you paid nothing in credit" would
    /// be noise on every one of them.
    /// </summary>
    [Fact]
    public void AReceiptThatUsedNoCredit_CarriesNoTenderLines()
    {
        var lines = Layout.Summary(Receipt(creditApplied: 0m));

        Assert.DoesNotContain(lines, l => l.Label == "Paid with credit");
        Assert.DoesNotContain(lines, l => l.Label == "Paid by card");
        Assert.Contains(lines, l => l is { Label: "Total", Value: "Kč2,000.00" });
    }

    [Fact]
    public void AReceiptSettledPartlyFromCredit_NamesBothTenders()
    {
        var lines = Layout.Summary(Receipt(total: 2000m, creditApplied: 500m));

        Assert.Contains(lines, l => l is { Label: "Paid with credit", Value: "-Kč500.00" });
        Assert.Contains(lines, l => l is { Label: "Paid by card", Value: "Kč1,500.00" });
    }

    /// <summary>
    /// THE FISCAL ONE. Total is the sale and the VAT block is its taxable base; neither moves because
    /// part of the sale was settled from a balance. Getting this wrong would under-declare VAT on every
    /// credit-bearing order.
    /// </summary>
    [Fact]
    public void CreditDoesNotShrinkTheTotalOrTheTaxableBase()
    {
        var withoutCredit = Layout.Summary(Receipt(total: 2000m, creditApplied: 0m));
        var withCredit = Layout.Summary(Receipt(total: 2000m, creditApplied: 500m));

        var total = withCredit.Single(l => l.Label == "Total");
        Assert.Equal(withoutCredit.Single(l => l.Label == "Total").Value, total.Value);

        var vatBefore = withoutCredit.Single(l => l.Label.StartsWith("VAT", StringComparison.Ordinal));
        var vatAfter = withCredit.Single(l => l.Label.StartsWith("VAT", StringComparison.Ordinal));
        Assert.Equal(vatBefore.Value, vatAfter.Value);

        var netBefore = withoutCredit.Single(l => l.Label.StartsWith("Subtotal", StringComparison.Ordinal));
        var netAfter = withCredit.Single(l => l.Label.StartsWith("Subtotal", StringComparison.Ordinal));
        Assert.Equal(netBefore.Value, netAfter.Value);
    }

    /// <summary>
    /// The card figure is what the customer will go looking for on their statement, so it is last and
    /// it is the emphasised one. Total stays bold as the size of the sale.
    /// </summary>
    [Fact]
    public void TheCardFigureIsLastAndEmphasised()
    {
        var lines = Layout.Summary(Receipt(total: 2000m, creditApplied: 500m));

        Assert.Equal("Paid by card", lines[^1].Label);
        Assert.True(lines[^1].IsBold);
        Assert.False(lines.Single(l => l.Label == "Paid with credit").IsBold);
    }

    /// <summary>
    /// A company that is not VAT-registered has no breakdown to show, and the tender lines have to
    /// work without one — this is the shape Cleansia ships in today.
    /// </summary>
    [Fact]
    public void TheTenderLinesWorkWithoutAVatBreakdown()
    {
        var lines = Layout.Summary(Receipt(total: 1000m, creditApplied: 800m, vatPayer: false));

        Assert.DoesNotContain(lines, l => l.Label.StartsWith("VAT", StringComparison.Ordinal));
        Assert.Contains(lines, l => l is { Label: "Total", Value: "Kč1,000.00" });
        Assert.Contains(lines, l => l is { Label: "Paid by card", Value: "Kč200.00" });
    }

    private sealed class ProbeLayout : DefaultReceiptLayoutBuilder
    {
        public IReadOnlyList<(string Label, string Value, bool IsBold)> Summary(ReceiptPdfData data) =>
            SummaryLines(data);
    }
}
