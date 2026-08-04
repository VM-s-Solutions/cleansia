namespace Cleansia.Infra.Services.Pdf.Models;

public record CountryInvoiceContext
{
    public bool VatRequired { get; init; }
    public decimal VatRate { get; init; }
    public bool DigitalSignatureRequired { get; init; }
    public string? EInvoiceFormat { get; init; }
    public string? LegalDisclaimerTemplate { get; init; }

    /// <summary>The payer's konstantní symbol for this country. Null ⇒ the field is omitted.</summary>
    public string? ConstantSymbol { get; init; }

    public Dictionary<string, string>? AdditionalFields { get; init; }

    /// <summary>
    /// The country's VAT setting governs what the platform charges its CUSTOMERS. It cannot make a
    /// cleaner a VAT payer, so a payout invoice carries VAT only when the SUPPLIER is registered — the
    /// rule lives here so the mapper and the PDF service cannot drift into disagreeing about it.
    /// </summary>
    public decimal VatFor(decimal subTotal, bool supplierIsVatPayer) =>
        VatRequired && supplierIsVatPayer ? subTotal * VatRate : 0m;
}
