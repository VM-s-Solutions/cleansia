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
    public required decimal Subtotal { get; init; }
    public required decimal Total { get; init; }
    public required string Currency { get; init; }
    public required string PaymentStatus { get; init; }
    public CompanyInfoData? Company { get; init; }
}

public record ReceiptLineItem(string Name, decimal Price);
