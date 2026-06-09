namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Country-agnostic data for fiscal registration. Each implementation maps
/// this into the payload expected by its fiscal authority API.
/// </summary>
/// <remarks>
/// <see cref="IdempotencyKey"/> is the explicit authority-side idempotency token (ADR-0004). It is
/// the natural token — the <see cref="ReceiptNumber"/> — but it is carried as a first-class field so a
/// provider's dedup behaviour is a stated contract rather than an implicit per-provider assumption:
/// the initial register and any recovery re-register for the same receipt present the SAME key, so an
/// idempotent authority collapses a redelivery onto the prior registration instead of burning a second
/// entry. Build instances via <see cref="Create"/> so the key cannot drift from the receipt number.
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
