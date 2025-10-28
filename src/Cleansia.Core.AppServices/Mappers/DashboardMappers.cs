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

    public static WeeklyOrderCount MapToWeeklyOrderCount(this IGrouping<(int Year, int Week), Order> group)
    {
        var weekStart = ISOWeek.ToDateTime(group.Key.Year, group.Key.Week, DayOfWeek.Monday);
        var completedCount = group.Count(o => o.GetCurrentOrderStatus() == Cleansia.Core.Domain.Enums.OrderStatus.Completed);
        var totalRevenue = group.Sum(o => o.TotalPrice);

        return new WeeklyOrderCount(
            Year: group.Key.Year,
            WeekNumber: group.Key.Week,
            WeekStartDate: weekStart,
            OrderCount: group.Count(),
            CompletedCount: completedCount,
            TotalRevenue: totalRevenue
        );
    }

    public static ServiceTypeCount MapToServiceTypeCount(
        this IGrouping<string, (string ServiceName, decimal Price)> group)
    {
        var count = group.Count();
        return new ServiceTypeCount(
            ServiceName: group.Key,
            OrderCount: count,
            AveragePrice: count > 0 ? group.Sum(s => s.Price) / count : 0,
            TotalRevenue: group.Sum(s => s.Price)
        );
    }
}
