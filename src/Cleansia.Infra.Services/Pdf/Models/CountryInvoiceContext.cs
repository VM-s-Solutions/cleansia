using Cleansia.Core.Domain.Enums;

namespace Cleansia.Infra.Services.Pdf.Models;

public record CountryInvoiceContext
{
    public bool VatRequired { get; init; }
    public decimal VatRate { get; init; }
    public bool DigitalSignatureRequired { get; init; }
    public string? EInvoiceFormat { get; init; }
    public string? LegalDisclaimerTemplate { get; init; }
    public string? LegalDisclaimerLanguageCode { get; init; }
    public LegalNoticeReviewStatus LegalDisclaimerReviewStatus { get; init; }

    /// <summary>The payer's konstantní symbol for this country. Null ⇒ the field is omitted.</summary>
    public string? ConstantSymbol { get; init; }

    public Dictionary<string, string>? AdditionalFields { get; init; }

    /// <summary>
    /// The cleaner's pay is GROSS — the stored total is what they receive in full, and the platform pays
    /// no tax on their behalf — so a VAT-registered supplier's invoice DECOMPOSES that total instead of
    /// adding to it: base = total ÷ (1 + rate), VAT = total − base. The printed total is therefore the
    /// stored total in both variants.
    /// <para>The country's VAT setting governs what the platform charges its CUSTOMERS and cannot make a
    /// cleaner a VAT payer, so this is zero unless the supplier is registered. The rule lives here alone
    /// so the mapper and the PDF service cannot drift into disagreeing about it.</para>
    /// </summary>
    public decimal VatWithinGross(decimal grossTotal, bool supplierIsVatPayer)
    {
        if (!VatRequired || !supplierIsVatPayer || VatRate <= 0m)
        {
            return 0m;
        }

        // One rounding, applied to the base, so base + VAT is exactly the gross on every amount.
        var netBase = Math.Round(grossTotal / (1m + VatRate), 2, MidpointRounding.AwayFromZero);
        return grossTotal - netBase;
    }

    /// <summary>
    /// The jurisdiction's own legal notice, or null when it has none the platform stands behind. Stored
    /// text below <see cref="LegalNoticeReviewStatus.BusinessSupplied"/> is withheld rather than printed:
    /// the fallback the layout supplies instead is honest about being generic, while an unchecked
    /// paragraph under a "PRÁVNÍ UPOZORNĚNÍ" heading claims a review that never happened.
    /// </summary>
    public string? ReviewedLegalNotice =>
        LegalDisclaimerReviewStatus != LegalNoticeReviewStatus.NotReviewed &&
        !string.IsNullOrWhiteSpace(LegalDisclaimerTemplate)
            ? LegalDisclaimerTemplate
            : null;
}
