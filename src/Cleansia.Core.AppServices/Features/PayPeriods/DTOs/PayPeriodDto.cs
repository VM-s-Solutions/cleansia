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
    string? Notes);
