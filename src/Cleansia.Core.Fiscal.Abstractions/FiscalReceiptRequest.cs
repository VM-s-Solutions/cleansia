namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Country-agnostic data for fiscal registration. Each implementation maps
/// this into the payload expected by its fiscal authority API.
/// </summary>
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
    string CountryCode);

public record FiscalLineItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate);
