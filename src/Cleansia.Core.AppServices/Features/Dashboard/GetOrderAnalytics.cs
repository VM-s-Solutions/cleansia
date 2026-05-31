#nullable enable
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets order analytics for an employee within a specified date range.
/// Provides order distribution by status, trends, and service type breakdown.
/// </summary>
public class GetOrderAnalytics
{
    public class Query : IRequest<OrderAnalyticsDto>
    {
        public required string EmployeeId { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
    }

    internal class Handler(
        IOrderRepository orderRepository)
        : IRequestHandler<Query, OrderAnalyticsDto>
    {
        public async Task<OrderAnalyticsDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var orders = await orderRepository
                .GetEmployeeOrdersByDateRangeAsync(request.EmployeeId, request.StartDate, request.EndDate, cancellationToken);

            var statusDistribution = orders
                .GroupBy(o => o.GetCurrentOrderStatus().ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            var weeklyTrends = orders
                .GroupBy(o => (Year: ISOWeek.GetYear(o.CleaningDateTime), Week: ISOWeek.GetWeekOfYear(o.CleaningDateTime)))
                .Select(g => g.MapToWeeklyOrderCount())
                .OrderBy(w => w.Year)
                .ThenBy(w => w.WeekNumber)
                .ToList();

            var serviceDistribution = orders
                .SelectMany(o => o.SelectedServices.Select(s => (
                    ServiceName: s.Service?.Name ?? "Unknown",
                    Price: o.TotalPrice
                )))
                .GroupBy(s => s.ServiceName)
                .Select(g => g.MapToServiceTypeCount())
                .OrderByDescending(s => s.OrderCount)
                .ToList();

            var totalOrders = orders.Count;
            // Authoritative completion / cancellation columns instead
            // of status-history reads — same answer today, cheaper
            // and unambiguous (no "is the last history row really
            // the terminal status?" question).
            var completedOrders = orders.Count(o => o.CompletedAt.HasValue);
            var cancelledOrders = orders.Count(o => o.CancelledAt.HasValue);
            var completionRate = CalculateCompletionRate(completedOrders, totalOrders);

            return new OrderAnalyticsDto(
                StatusDistribution: statusDistribution,
                WeeklyTrends: weeklyTrends,
                ServiceDistribution: serviceDistribution,
                TotalOrders: totalOrders,
                CompletionRate: completionRate,
                CancelledOrders: cancelledOrders
            );
        }

        private static double CalculateCompletionRate(int completedOrders, int totalOrders)
        {
            return totalOrders > 0 ? (double)completedOrders / totalOrders * 100 : 0;
        }
    }
}
