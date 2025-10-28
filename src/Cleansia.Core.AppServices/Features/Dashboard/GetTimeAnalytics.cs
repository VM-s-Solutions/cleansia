#nullable enable
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cleansia.Core.AppServices.Features.Dashboard;

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

            var totalMinutesWorked = orders.Sum(o => o.EstimatedTime);
            var totalOrders = orders.Count;
            var averageMinutesPerOrder = CalculateAverageMinutesPerOrder(totalMinutesWorked, totalOrders);

            return new TimeAnalyticsDto(
                DailyBreakdown: dailyBreakdown,
                WeeklyBreakdown: weeklyBreakdown,
                ByServiceType: serviceBreakdown,
                TotalMinutesWorked: totalMinutesWorked,
                AverageMinutesPerOrder: averageMinutesPerOrder,
                EfficiencyRate: DashboardConstants.DefaultEfficiencyRate,
                TotalOrders: totalOrders
            );
        }

        private static int CalculateAverageMinutesPerOrder(int totalMinutes, int orderCount)
        {
            return orderCount > 0 ? totalMinutes / orderCount : 0;
        }
    }
}
