namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

public record TimeAnalyticsDto(
    IEnumerable<DailyTimeSpent> DailyBreakdown,
    IEnumerable<WeeklyTimeSpent> WeeklyBreakdown,
    IEnumerable<ServiceTimeBreakdown> ByServiceType,
    int TotalMinutesWorked,
    int AverageMinutesPerOrder,
    double EfficiencyRate,
    int TotalOrders);

public record DailyTimeSpent(
    DateTime Date,
    int EstimatedMinutes,
    int ActualMinutes,
    int OrdersCompleted,
    string DayOfWeek);

public record WeeklyTimeSpent(
    int Year,
    int WeekNumber,
    DateTime WeekStartDate,
    int TotalMinutes,
    int OrdersCompleted,
    int AverageMinutesPerOrder);

public record ServiceTimeBreakdown(
    string ServiceName,
    int TotalMinutes,
    int OrderCount,
    int AverageMinutesPerOrder);
