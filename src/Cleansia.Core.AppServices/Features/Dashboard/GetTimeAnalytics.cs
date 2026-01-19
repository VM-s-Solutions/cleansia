#nullable enable
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets time analytics for an employee within a specified date range.
/// All metrics are calculated from real order data.
/// </summary>
public class GetTimeAnalytics
{
    public class Query : IRequest<TimeAnalyticsDto>
    {
        public required string EmployeeId { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
    }

    internal class Handler(IOrderRepository orderRepository) : IRequestHandler<Query, TimeAnalyticsDto>
    {
        public async Task<TimeAnalyticsDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var orders = await orderRepository
                .GetCompletedOrdersByDateRange(request.EmployeeId, request.StartDate, request.EndDate)
                .ToListAsync(cancellationToken);

            var dailyBreakdown = orders
                .GroupBy(o => o.CleaningDateTime.Date)
                .Select(g => g.MapToDailyTimeSpent())
                .OrderBy(d => d.Date)
                .ToList();

            var weeklyBreakdown = orders
                .GroupBy(o => (Year: ISOWeek.GetYear(o.CleaningDateTime), Week: ISOWeek.GetWeekOfYear(o.CleaningDateTime)))
                .Select(g => g.MapToWeeklyTimeSpent())
                .OrderBy(w => w.Year)
                .ThenBy(w => w.WeekNumber)
                .ToList();

            var serviceBreakdown = orders
                .SelectMany(o => o.SelectedServices.Select(s => (
                    ServiceName: s.Service?.Name,
                    EstimatedTime: o.EstimatedTime
                )))
                .GroupBy(s => s.ServiceName)
                .Select(g => g.MapToServiceTimeBreakdown())
                .OrderByDescending(s => s.TotalMinutes)
                .ToList();

            var totalOrders = orders.Count;

            // Calculate real metrics from actual completion times
            var (totalMinutesWorked, averageMinutesPerOrder) = CalculateActualTimeMetrics(orders);
            var efficiencyRate = CalculateEfficiencyRate(orders);

            return new TimeAnalyticsDto(
                DailyBreakdown: dailyBreakdown,
                WeeklyBreakdown: weeklyBreakdown,
                ByServiceType: serviceBreakdown,
                TotalMinutesWorked: totalMinutesWorked,
                AverageMinutesPerOrder: averageMinutesPerOrder,
                EfficiencyRate: efficiencyRate,
                TotalOrders: totalOrders
            );
        }

        /// <summary>
        /// Calculates total and average time metrics using actual completion times when available.
        /// </summary>
        private static (int TotalMinutes, int AverageMinutes) CalculateActualTimeMetrics(List<Order> orders)
        {
            if (orders.Count == 0)
                return (0, 0);

            var ordersWithActualTime = orders.Where(o => o.ActualCompletionTime.HasValue).ToList();

            if (ordersWithActualTime.Count > 0)
            {
                var totalActualMinutes = ordersWithActualTime.Sum(o => o.ActualCompletionTime!.Value);
                var averageActualMinutes = totalActualMinutes / ordersWithActualTime.Count;
                return (totalActualMinutes, averageActualMinutes);
            }

            // Fall back to estimated times if no actual times recorded
            var totalEstimatedMinutes = orders.Sum(o => o.EstimatedTime);
            var averageEstimatedMinutes = totalEstimatedMinutes / orders.Count;
            return (totalEstimatedMinutes, averageEstimatedMinutes);
        }

        /// <summary>
        /// Calculates efficiency rate as the ratio of estimated time to actual time.
        /// A rate of 100% means completing exactly on time.
        /// A rate above 100% means completing faster than estimated.
        /// A rate below 100% means taking longer than estimated.
        /// </summary>
        private static double CalculateEfficiencyRate(List<Order> orders)
        {
            var ordersWithActualTime = orders
                .Where(o => o.ActualCompletionTime.HasValue && o.ActualCompletionTime.Value > 0)
                .ToList();

            if (ordersWithActualTime.Count == 0)
                return 100.0; // Default to 100% if no actual times recorded

            var totalEstimated = ordersWithActualTime.Sum(o => o.EstimatedTime);
            var totalActual = ordersWithActualTime.Sum(o => o.ActualCompletionTime!.Value);

            if (totalActual == 0)
                return 100.0;

            // Efficiency = (Estimated / Actual) * 100
            return (double)totalEstimated / totalActual * 100;
        }
    }
}
