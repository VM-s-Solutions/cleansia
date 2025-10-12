namespace Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;

public record EmployeeInvoiceDto(
    string Id,
    string EmployeeId,
    string EmployeeName,
    string PayPeriodId,
    string PayPeriodLabel,
    string InvoiceNumber,
    string VariableSymbol,
    int TotalOrders,
    decimal SubTotal,
    decimal BonusAmount,
    decimal DeductionAmount,
    decimal TotalAmount,
    string CurrencyCode,
    string Status,
    string? PdfBlobName,
    DateTime GeneratedAt,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? PaidAt,
    string? AdminNotes,
    string? BankTransferNote);
