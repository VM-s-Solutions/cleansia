using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;

public record EmployeeInvoiceDto(
    string Id,
    string EmployeeId,
    string EmployeeName,
    string PayPeriodId,
    string PayPeriodLabel,
    string InvoiceNumber,
    string? VariableSymbol,
    string? PaymentReference,
    int TotalOrders,
    decimal SubTotal,
    decimal BonusAmount,
    decimal DeductionAmount,
    decimal TotalAmount,
    string CurrencyCode,
    EmployeeInvoiceStatus Status,
    string? PdfBlobName,
    DateTime GeneratedAt,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? PaidAt,
    string? AdminNotes,
    string? BankTransferNote);
