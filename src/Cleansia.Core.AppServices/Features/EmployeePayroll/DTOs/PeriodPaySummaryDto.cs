namespace Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;

public record PeriodPaySummaryDto(
    string PayPeriodId,
    string PayPeriodLabel,
    string EmployeeId,
    string EmployeeName,
    int TotalOrders,
    decimal TotalBasePay,
    decimal TotalExtrasPay,
    decimal TotalExpensesPay,
    decimal TotalBonusPay,
    decimal TotalDeductionPay,
    decimal GrandTotal,
    bool HasInvoice,
    string? InvoiceId,
    IEnumerable<OrderEmployeePayDto> OrderPays);
