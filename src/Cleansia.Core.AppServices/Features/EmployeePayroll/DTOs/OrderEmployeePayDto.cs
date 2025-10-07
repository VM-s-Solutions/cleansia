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
    DateTime CreatedOn);
