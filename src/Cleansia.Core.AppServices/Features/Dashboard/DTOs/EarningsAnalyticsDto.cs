namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

public record EarningsAnalyticsDto(
    IEnumerable<MonthlyEarning> MonthlyEarnings,
    EarningsBreakdown Breakdown,
    decimal TotalEarnings,
    decimal AverageMonthlyEarnings,
    MonthlyEarning? HighestMonth,
    MonthlyEarning? LowestMonth,
    decimal GrowthPercentage);

public record MonthlyEarning(
    int Year,
    int Month,
    decimal Amount,
    string MonthName);

public record EarningsBreakdown(
    decimal SubTotal,
    decimal Bonuses,
    decimal Deductions,
    decimal TotalAmount,
    Dictionary<string, decimal> ByServiceType);
