using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Orders;
using System.Globalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class DashboardMappers
{
    public static MonthlyEarning MapToMonthlyEarning(this IGrouping<(int Year, int Month), EmployeeInvoice> group)
    {
        return new MonthlyEarning(
            Year: group.Key.Year,
            Month: group.Key.Month,
            Amount: group.Sum(i => i.TotalAmount),
            MonthName: CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(group.Key.Month)
        );
    }

    public static EarningsBreakdown MapToEarningsBreakdown(
        this IEnumerable<EmployeeInvoice> invoices,
        Dictionary<string, decimal> serviceBreakdown)
    {
        return new EarningsBreakdown(
            SubTotal: invoices.Sum(i => i.SubTotal),
            Bonuses: invoices.Sum(i => i.BonusAmount),
            Deductions: invoices.Sum(i => i.DeductionAmount),
            TotalAmount: invoices.Sum(i => i.TotalAmount),
            ByServiceType: serviceBreakdown
        );
    }

    public static DailyTimeSpent MapToDailyTimeSpent(this IGrouping<DateTime, Order> group)
    {
        return new DailyTimeSpent(
            Date: group.Key,
            EstimatedMinutes: group.Sum(o => o.EstimatedTime),
            ActualMinutes: group.Sum(o => o.EstimatedTime),
            OrdersCompleted: group.Count(),
            DayOfWeek: group.Key.ToString("dddd", CultureInfo.CurrentCulture)
        );
    }

    public static WeeklyTimeSpent MapToWeeklyTimeSpent(this IGrouping<(int Year, int Week), Order> group)
    {
        var weekStart = ISOWeek.ToDateTime(group.Key.Year, group.Key.Week, DayOfWeek.Monday);
        var totalMinutes = group.Sum(o => o.EstimatedTime);
        var orderCount = group.Count();

        return new WeeklyTimeSpent(
            Year: group.Key.Year,
            WeekNumber: group.Key.Week,
            WeekStartDate: weekStart,
            TotalMinutes: totalMinutes,
            OrdersCompleted: orderCount,
            AverageMinutesPerOrder: orderCount > 0 ? totalMinutes / orderCount : 0
        );
    }

    public static ServiceTimeBreakdown MapToServiceTimeBreakdown(
        this IGrouping<string, (string ServiceName, int EstimatedTime)> group)
    {
        return new ServiceTimeBreakdown(
            ServiceName: group.Key,
            TotalMinutes: group.Sum(s => s.EstimatedTime),
            OrderCount: group.Count(),
            AverageMinutesPerOrder: group.Any() ? group.Sum(s => s.EstimatedTime) / group.Count() : 0
        );
    }

    /// <summary>
    /// Counts cover every order in the week; TotalRevenue covers only the orders in <paramref name="currencyId"/>,
    /// the currency the dashboard labels it with. A count has no unit; a sum does.
    /// </summary>
    public static WeeklyOrderCount MapToWeeklyOrderCount(this IGrouping<(int Year, int Week), Order> group, string currencyId)
    {
        var weekStart = ISOWeek.ToDateTime(group.Key.Year, group.Key.Week, DayOfWeek.Monday);
        var completedCount = group.Count(o => o.GetCurrentOrderStatus() == Cleansia.Core.Domain.Enums.OrderStatus.Completed);
        var totalRevenue = group.Where(o => o.CurrencyId == currencyId).Sum(o => o.TotalPrice);

        return new WeeklyOrderCount(
            Year: group.Key.Year,
            WeekNumber: group.Key.Week,
            WeekStartDate: weekStart,
            OrderCount: group.Count(),
            CompletedCount: completedCount,
            TotalRevenue: totalRevenue
        );
    }

    /// <summary>
    /// Counts cover every order; the two money columns cover only orders in <paramref name="currencyId"/>,
    /// the currency the dashboard labels them with. A count has no unit; a sum does.
    /// </summary>
    public static ServiceTypeCount MapToServiceTypeCount(
        this IGrouping<string, (string ServiceName, string CurrencyId, decimal Price)> group,
        string currencyId)
    {
        var priced = group.Where(s => s.CurrencyId == currencyId).ToList();
        return new ServiceTypeCount(
            ServiceName: group.Key,
            OrderCount: group.Count(),
            AveragePrice: priced.Count > 0 ? priced.Sum(s => s.Price) / priced.Count : 0,
            TotalRevenue: priced.Sum(s => s.Price)
        );
    }
}
