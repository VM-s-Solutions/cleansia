using Cleansia.Core.Domain.Enums;

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
    string? DeductionReason = null,
    decimal DirtinessPay = 0m,
    /// <summary>The job, or the cleaner's share of a late-cancellation or lockout fee collected on a cancelled one.</summary>
    PayLineType LineType = PayLineType.Job);
