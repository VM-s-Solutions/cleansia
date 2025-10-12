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
    public Dictionary<string, string>? AdditionalFields { get; init; }
}
