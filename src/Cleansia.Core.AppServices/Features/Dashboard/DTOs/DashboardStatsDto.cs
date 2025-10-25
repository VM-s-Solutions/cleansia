namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

/// <summary>
/// Dashboard statistics data transfer object.
/// Contains all metrics needed for the employee dashboard in a single response.
/// </summary>
public record DashboardStatsDto(
    int AvailableOrdersCount,
    int MyActiveOrdersCount,
    int ThisMonthCompletedOrders,
    int LastMonthCompletedOrders,
    decimal CurrentPeriodEarnings,
    string? LatestInvoiceStatus
);
