namespace Cleansia.Infra.Services.Pdf.Models;

public record CountryInvoiceContext
{
    public bool VatRequired { get; init; }
    public decimal VatRate { get; init; }
    public bool DigitalSignatureRequired { get; init; }
    public string? EInvoiceFormat { get; init; }
    public string? LegalDisclaimerTemplate { get; init; }
    public Dictionary<string, string>? AdditionalFields { get; init; }
}
