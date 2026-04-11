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
    public List<string> Extras { get; init; } = [];
    public required decimal Total { get; init; }
    public required string Currency { get; init; }
    public required string PaymentStatus { get; init; }
    public string? PaymentType { get; init; }
    public string? CleaningDate { get; init; }
    public int? Rooms { get; init; }
    public int? Bathrooms { get; init; }
    public int? EstimatedTime { get; init; }
    public CompanyInfoData? Company { get; init; }

    // VAT breakdown — populated from Order at receipt generation time.
    // When IsVatPayer is false, NonVatPayerNotice is shown instead of VAT rows.
    public bool IsVatPayer { get; init; }
    public decimal? NetAmount { get; init; }
    public decimal? VatAmount { get; init; }
    public decimal? VatRate { get; init; }
    public string? NonVatPayerNotice { get; init; }

    // Fiscal registration — set after fiscal authority responds.
    // Null when the country has no fiscal system or registration failed.
    public string? FiscalProviderKey { get; set; }
    public string? FiscalCode { get; set; }
    public string? FiscalRegisteredAt { get; set; }
}

public record ReceiptLineItem(string Name, decimal Price);
