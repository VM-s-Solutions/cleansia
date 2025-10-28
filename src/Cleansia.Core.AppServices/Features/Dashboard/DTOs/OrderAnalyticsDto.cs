namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

public record OrderAnalyticsDto(
    Dictionary<string, int> StatusDistribution,
    IEnumerable<WeeklyOrderCount> WeeklyTrends,
    IEnumerable<ServiceTypeCount> ServiceDistribution,
    int TotalOrders,
    double CompletionRate,
    int CancelledOrders);

public record WeeklyOrderCount(
    int Year,
    int WeekNumber,
    DateTime WeekStartDate,
    int OrderCount,
    int CompletedCount,
    decimal TotalRevenue);

public record ServiceTypeCount(
    string ServiceName,
    int OrderCount,
    decimal AveragePrice,
    decimal TotalRevenue);
