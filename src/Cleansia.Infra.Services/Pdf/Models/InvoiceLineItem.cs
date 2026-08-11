namespace Cleansia.Infra.Services.Pdf.Models;

public record InvoiceLineItem
{
    public required string OrderNumber { get; init; }
    public required DateTime PerformedOn { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
}
