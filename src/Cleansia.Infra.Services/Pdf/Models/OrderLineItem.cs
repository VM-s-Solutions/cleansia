namespace Cleansia.Infra.Services.Pdf.Models;

public record OrderLineItem
{
    public required string OrderNumber { get; init; }
    public required DateTime CompletedAt { get; init; }
    public required decimal BasePay { get; init; }
    public required decimal ExtrasPay { get; init; }
    public required decimal ExpensesPay { get; init; }
    public required decimal TotalPay { get; init; }
}
