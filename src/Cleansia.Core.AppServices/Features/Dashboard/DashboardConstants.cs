namespace Cleansia.Core.AppServices.Features.Dashboard;

public static class DashboardConstants
{
    /// <summary>
    /// Default target number of orders per month for employees
    /// </summary>
    public const int DefaultMonthlyOrdersTarget = 20;

    /// <summary>
    /// Default efficiency rate percentage when actual time tracking is not available
    /// </summary>
    public const double DefaultEfficiencyRate = 100.0;

    /// <summary>
    /// Default best efficiency score (placeholder value)
    /// </summary>
    public const double DefaultBestEfficiencyScore = 98.5;

    /// <summary>
    /// Starting date for all-time statistics calculation
    /// </summary>
    public static readonly DateTime AllTimeStartDate = DateTime.SpecifyKind(new DateTime(2020, 1, 1), DateTimeKind.Utc);

    /// <summary>
    /// Weight of completion percentage in efficiency score calculation
    /// </summary>
    public const double CompletionPercentageWeight = 0.6;

    /// <summary>
    /// Weight of on-time completion rate in efficiency score calculation
    /// </summary>
    public const double OnTimeCompletionWeight = 0.4;
}
