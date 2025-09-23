#nullable enable

namespace Cleansia.Core.AppServices.Features.Orders.Filters;

public record OrderFilter(
    string? Id,
    bool? IsActive,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? DisplayOrderNumber,
    string? EmployeeId,
    string? PackageId,
    DateTime? CleaningDateFrom,
    DateTime? CleaningDateTo,
    int[]? PaymentStatuses,
    int[]? PaymentTypes,
    decimal? MinTotalPrice,
    decimal? MaxTotalPrice
);