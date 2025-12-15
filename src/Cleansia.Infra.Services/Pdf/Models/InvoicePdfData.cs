namespace Cleansia.Infra.Services.Pdf.Models;

public record InvoicePdfData
{
    public required string InvoiceNumber { get; init; }
    public required string VariableSymbol { get; init; }
    public required DateTime GeneratedAt { get; init; }

    public required string EmployeeName { get; init; }
    public required string EmployeeAddress { get; init; }
    public required string EmployeeEmail { get; init; }

    public required string PayPeriodStart { get; init; }
    public required string PayPeriodEnd { get; init; }

    public required decimal SubTotal { get; init; }
    public required decimal BonusAmount { get; init; }
    public required decimal DeductionAmount { get; init; }
    public required decimal VatAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string CurrencyCode { get; init; }
    public required string CurrencySymbol { get; init; }

    public required List<OrderLineItem> Orders { get; init; }

    public string? LegalDisclaimer { get; init; }
    public CompanyInfoData? Company { get; init; }
}

public record CompanyInfoData
{
    public required string LegalName { get; init; }
    public required string TradingName { get; init; }
    public string? Tagline { get; init; }

    public required string RegistrationNumber { get; init; }
    public string? VatNumber { get; init; }

    public required string Street { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
    public required string Address { get; init; }

    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public required string ContactInfo { get; init; }

    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? Iban { get; init; }
    public string? Swift { get; init; }
}
