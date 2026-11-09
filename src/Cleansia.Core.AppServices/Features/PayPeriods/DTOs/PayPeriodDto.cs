namespace Cleansia.Core.AppServices.Features.PayPeriods.DTOs;

public record PayPeriodDto(
    string Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string PeriodLabel,
    int DurationDays,
    DateTime? ClosedAt,
    string? ClosedBy,
    DateTime? PaidAt,
    string? Notes,
    DateOnly LatestEndDate,
    // Company-wide, so only the admin's single-period read fills them: the partner host lists this DTO to
    // every cleaner.
    int? InvoiceCount = null,
    int? UnpaidInvoiceCount = null,
    int? UninvoicedPayCount = null);
