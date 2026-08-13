namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Country-agnostic data for fiscal registration; each implementation maps it to its authority's payload.
/// </summary>
/// <remarks>
/// The idempotency key is the natural token — the receipt number — but is carried as a first-class field
/// so <b>a provider's dedup behaviour is a stated contract rather than an implicit assumption</b>. The
/// initial register and any recovery re-register present the SAME key. Build via <c>Create</c> so the key
/// cannot drift from the receipt number. → /architecture/fiscal-compliance
/// </remarks>
public record FiscalReceiptRequest(
    string ReceiptNumber,
    DateTime IssuedAt,
    decimal TotalAmount,
    decimal? VatAmount,
    string CurrencyCode,
    string CompanyLegalName,
    string CompanyRegistrationNumber,
    string? CompanyVatNumber,
    string CustomerName,
    string? CustomerEmail,
    IReadOnlyList<FiscalLineItem> LineItems,
    string PaymentMethod,
    string CountryCode,
    string IdempotencyKey)
{
    public static FiscalReceiptRequest Create(
        string receiptNumber,
        DateTime issuedAt,
        decimal totalAmount,
        decimal? vatAmount,
        string currencyCode,
        string companyLegalName,
        string companyRegistrationNumber,
        string? companyVatNumber,
        string customerName,
        string? customerEmail,
        IReadOnlyList<FiscalLineItem> lineItems,
        string paymentMethod,
        string countryCode) =>
        new(
            receiptNumber,
            issuedAt,
            totalAmount,
            vatAmount,
            currencyCode,
            companyLegalName,
            companyRegistrationNumber,
            companyVatNumber,
            customerName,
            customerEmail,
            lineItems,
            paymentMethod,
            countryCode,
            IdempotencyKey: receiptNumber);
}

public record FiscalLineItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate);
