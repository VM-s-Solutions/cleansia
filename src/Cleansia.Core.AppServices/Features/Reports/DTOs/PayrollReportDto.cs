namespace Cleansia.Core.AppServices.Features.Reports.DTOs;

public record PayrollReportDto(
    decimal TotalPayroll,
    decimal AveragePayPerEmployee,
    int TotalInvoices,
    int PendingInvoices,
    int ApprovedInvoices,
    int PaidInvoices,
    int CancelledInvoices,
    decimal TotalBonuses,
    decimal TotalDeductions,
    IEnumerable<EmployeePayrollSummary> EmployeeSummaries,
    IEnumerable<PayrollByStatus> PayrollByStatus,
    IEnumerable<MonthlyPayroll> MonthlyPayroll);

public record EmployeePayrollSummary(
    string EmployeeId,
    string EmployeeName,
    int TotalOrders,
    int InvoiceCount,
    decimal SubTotal,
    decimal BonusAmount,
    decimal DeductionAmount,
    decimal TotalAmount);

public record PayrollByStatus(
    string StatusCode,
    string StatusName,
    int InvoiceCount,
    decimal TotalAmount);

public record MonthlyPayroll(
    int Year,
    int Month,
    string MonthName,
    decimal TotalAmount,
    int InvoiceCount);