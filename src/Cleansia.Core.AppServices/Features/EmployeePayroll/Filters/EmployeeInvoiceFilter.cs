#nullable enable

namespace Cleansia.Core.AppServices.Features.EmployeePayroll.Filters;

public record EmployeeInvoiceFilter(
    string? EmployeeId,
    string? PayPeriodId,
    int[]? Statuses
);
