using Cleansia.Core.Domain.Enums;

namespace Cleansia.Infra.Services.Pdf.Models;

public record ReceiptPdfData
{
    public required string ReceiptNumber { get; init; }
    public required string OrderNumber { get; init; }
    public required string IssuedDate { get; init; }
    public required string CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerAddress { get; init; }
    public required List<ReceiptLineItem> Services { get; init; }
    public required List<ReceiptLineItem> Packages { get; init; }

    /// <summary>Extras, each at the price it was bought at.</summary>
    public List<ReceiptLineItem> Extras { get; init; } = [];

    /// <summary>The express surcharge charged on this booking, zero when none was.</summary>
    public decimal ExpressSurcharge { get; init; }

    /// <summary>
    /// The three discount sources, separately, because each is a different promise to the customer.
    /// Measured against the CHARGED price (surcharge included), which is what makes the item lines and
    /// the total reconcile.
    /// </summary>
    public decimal TierDiscount { get; init; }
    public decimal MembershipDiscount { get; init; }
    public decimal PromoDiscount { get; init; }

    public required decimal Total { get; init; }
    public required string Currency { get; init; }

    /// <summary>
    /// The enum values, not their names: the word for each is chosen from <see cref="ReceiptLabels"/> in
    /// the document's language.
    /// </summary>
    public required PaymentStatus PaymentStatus { get; init; }
    public required PaymentType PaymentType { get; init; }

    /// <summary>
    /// The language the document is written in — the customer's, as recorded on the receipt row, so a
    /// re-render reproduces the document rather than re-deciding it.
    /// </summary>
    public string LanguageCode { get; init; } = "en";

    public string? CleaningDate { get; init; }
    public int? Rooms { get; init; }
    public int? Bathrooms { get; init; }
    public int? EstimatedTime { get; init; }
    public CompanyInfoData? Company { get; init; }

    // Tender split. Total stays the SALE and is what the fiscal authority registers - credit is a
    // tender, not a discount, so the taxable base does not move. These two say how the sale was
    // SETTLED, which is a different question and the one a customer holding the receipt is asking:
    // "why is my card statement 500 lighter than this number?".
    //
    // Both zero on every receipt that used no credit, and the layout omits the rows entirely then, so
    // no shipped receipt changes.
    public decimal CreditApplied { get; init; }
    public decimal AmountDueOnCard { get; init; }

    // VAT breakdown — populated from Order at receipt generation time.
    // When IsVatPayer is false, the statutory non-payer notice is shown instead of VAT rows — and the
    // company's VAT number is withheld, because a document cannot both disclaim VAT registration and
    // print a VAT registration.
    public bool IsVatPayer { get; init; }
    public decimal? NetAmount { get; init; }
    public decimal? VatAmount { get; init; }
    public decimal? VatRate { get; init; }

    // Fiscal registration — set after fiscal authority responds.
    // Null when the country has no fiscal system or registration failed.
    public string? FiscalProviderKey { get; set; }
    public string? FiscalCode { get; set; }
    public string? FiscalRegisteredAt { get; set; }
}

public record ReceiptLineItem(string Name, decimal Price);
