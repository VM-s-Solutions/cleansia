namespace Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;

public record OrderEmployeePayDto(
    string Id,
    string OrderId,
    string OrderNumber,
    string EmployeeId,
    string EmployeeName,
    string PayPeriodId,
    string PayPeriodLabel,
    decimal BasePay,
    decimal ExtrasPay,
    decimal ExpensesPay,
    decimal BonusPay,
    decimal DeductionPay,
    decimal TotalPay,
    string? PayBreakdown,
    bool IsApproved,
    DateTime CreatedOn,
    string? CurrencyCode = null,
    /// <summary>Why the cleaner was charged when a dispute found them at fault; null otherwise.</summary>
    string? DeductionReason = null);
