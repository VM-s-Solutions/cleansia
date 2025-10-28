namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

public record ProductivityMetricsDto(
    int OrdersCompleted,
    int OrdersTarget,
    double CompletionPercentage,
    double AverageCompletionTimeMinutes,
    double OnTimeCompletionRate,
    double EfficiencyScore,
    PersonalBests PersonalBests);

public record PersonalBests(
    MonthlyEarning? HighestEarningMonth,
    int MostOrdersInDay,
    DateTime? MostOrdersDate,
    int MostOrdersInMonth,
    int CurrentMonthYear,
    double BestEfficiencyScore);
