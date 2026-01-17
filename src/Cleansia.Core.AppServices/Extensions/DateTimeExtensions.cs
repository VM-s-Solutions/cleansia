namespace Cleansia.Core.AppServices.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Gets the start of the month for a given date
    /// </summary>
    public static DateTime GetMonthStart(this DateTime date)
    {
        return DateTime.SpecifyKind(new DateTime(date.Year, date.Month, 1), DateTimeKind.Utc);
    }

    /// <summary>
    /// Gets the end of the month for a given date
    /// </summary>
    public static DateTime GetMonthEnd(this DateTime date)
    {
        var monthStart = date.GetMonthStart();
        return DateTime.SpecifyKind(
            monthStart.AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59),
            DateTimeKind.Utc
        );
    }

    /// <summary>
    /// Gets the start and end dates for the previous month relative to the given date
    /// </summary>
    public static (DateTime Start, DateTime End) GetPreviousMonthRange(this DateTime date)
    {
        var previousMonth = date.AddMonths(-1);
        var previousMonthStart = DateTime.SpecifyKind(new DateTime(previousMonth.Year, previousMonth.Month, 1), DateTimeKind.Utc);
        var previousMonthEnd = previousMonthStart.GetMonthEnd();
        return (previousMonthStart, previousMonthEnd);
    }

    /// <summary>
    /// Gets the start and end dates for the current month relative to the given date
    /// </summary>
    public static (DateTime Start, DateTime End) GetCurrentMonthRange(this DateTime date)
    {
        var monthStart = date.GetMonthStart();
        var monthEnd = date.GetMonthEnd();
        return (monthStart, monthEnd);
    }
}
