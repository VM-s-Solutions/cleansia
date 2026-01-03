#nullable enable

using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll.Filters;

public record EmployeeInvoiceFilter(
    string? EmployeeId,
    string? PayPeriodId,
    EmployeeInvoiceStatus[]? Statuses
);
