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
    string? ExcludeEmployeeId,
    string? CurrencyId = null,
    // The account the orders were booked on. Admin-only, like the customer PII terms above: a cleaner
    // must not be able to enumerate a customer's bookings by id. A guest booking carries no UserId and
    // is never this account's, whatever e-mail it names (SubjectOrders).
    string? UserId = null);