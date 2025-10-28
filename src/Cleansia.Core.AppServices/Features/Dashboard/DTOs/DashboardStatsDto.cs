namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

public record DashboardStatsDto(
    int AvailableOrdersCount,
    int MyActiveOrdersCount,
    int ThisMonthCompletedOrders,
    int LastMonthCompletedOrders,
    decimal CurrentPeriodEarnings,
    string? LatestInvoiceStatus);
