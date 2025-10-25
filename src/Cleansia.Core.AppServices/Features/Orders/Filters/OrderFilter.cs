#nullable enable

using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Orders.Filters;

public record OrderFilter(
    string? Id,
    bool? IsActive,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? DisplayOrderNumber,
    string? EmployeeId,
    DateTime? CleaningDateFrom,
    DateTime? CleaningDateTo,
    IEnumerable<PaymentStatus>? PaymentStatuses,
    IEnumerable<PaymentType>? PaymentTypes,
    decimal? MinTotalPrice,
    decimal? MaxTotalPrice,
    IEnumerable<OrderStatus>? OrderStatuses,
    bool? HasAvailableSpots,
    bool? IsUnassigned,
    string? ExcludeEmployeeId);