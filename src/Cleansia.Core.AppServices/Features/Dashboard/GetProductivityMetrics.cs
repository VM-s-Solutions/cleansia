#nullable enable
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets productivity metrics for an employee.
/// Provides KPIs, targets, and personal best achievements.
/// </summary>
public class GetProductivityMetrics
{
    public class Query : IRequest<ProductivityMetricsDto>
    {
        public required string EmployeeId { get; init; }
    }

    internal class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository)
        : IRequestHandler<Query, ProductivityMetricsDto>
    {
        public async Task<ProductivityMetricsDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow;
            var (currentMonthStart, currentMonthEnd) = today.GetCurrentMonthRange();

            var completedSpec = DashboardSpecifications.CreateCompletedOrdersSpec(
                request.EmployeeId, currentMonthStart, currentMonthEnd);
            var ordersCompleted = await orderRepository.GetCountAsync(
                completedSpec.SatisfiedBy(), cancellationToken);

            var completedOrders = await orderRepository
                .GetCompletedOrdersByDateRange(request.EmployeeId, currentMonthStart, currentMonthEnd)
                .ToListAsync(cancellationToken);

            var totalMinutes = completedOrders.Sum(o => o.EstimatedTime);
            var averageCompletionTimeMinutes = CalculateAverageCompletionTime(totalMinutes, completedOrders.Count);
            var completionPercentage = CalculateCompletionPercentage(ordersCompleted, DashboardConstants.DefaultMonthlyOrdersTarget);
            var efficiencyScore = CalculateEfficiencyScore(completionPercentage, DashboardConstants.DefaultEfficiencyRate);
            var personalBests = await CalculatePersonalBestsAsync(request.EmployeeId, cancellationToken);

            return new ProductivityMetricsDto(
                OrdersCompleted: ordersCompleted,
                OrdersTarget: DashboardConstants.DefaultMonthlyOrdersTarget,
                CompletionPercentage: completionPercentage,
                AverageCompletionTimeMinutes: averageCompletionTimeMinutes,
                OnTimeCompletionRate: DashboardConstants.DefaultEfficiencyRate,
                EfficiencyScore: efficiencyScore,
                PersonalBests: personalBests
            );
        }

        private static double CalculateAverageCompletionTime(int totalMinutes, int orderCount)
        {
            return orderCount > 0 ? (double)totalMinutes / orderCount : 0;
        }

        private static double CalculateCompletionPercentage(int ordersCompleted, int ordersTarget)
        {
            return ordersTarget > 0 ? (double)ordersCompleted / ordersTarget * 100 : 0;
        }

        private static double CalculateEfficiencyScore(double completionPercentage, double onTimeRate)
        {
            return (completionPercentage * DashboardConstants.CompletionPercentageWeight) +
                   (onTimeRate * DashboardConstants.OnTimeCompletionWeight);
        }

        private async Task<PersonalBests> CalculatePersonalBestsAsync(string employeeId, CancellationToken cancellationToken)
        {
            var allTimeEnd = DateTime.UtcNow;
            var allOrders = await orderRepository
                .GetCompletedOrdersByDateRange(employeeId, DashboardConstants.AllTimeStartDate, allTimeEnd)
                .ToListAsync(cancellationToken);

            var invoices = await employeeInvoiceRepository
                .GetInvoicesByDateRange(employeeId, DashboardConstants.AllTimeStartDate, allTimeEnd)
                .ToListAsync(cancellationToken);

            var highestEarningMonth = invoices
                .GroupBy(i => (i.GeneratedAt.Year, i.GeneratedAt.Month))
                .Select(g => g.MapToMonthlyEarning())
                .OrderByDescending(m => m.Amount)
                .FirstOrDefault();

            var dailyOrders = allOrders
                .GroupBy(o => o.CleaningDateTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderByDescending(d => d.Count)
                .FirstOrDefault();

            var monthlyOrders = allOrders
                .GroupBy(o => new { o.CleaningDateTime.Year, o.CleaningDateTime.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderByDescending(m => m.Count)
                .FirstOrDefault();

            return new PersonalBests(
                HighestEarningMonth: highestEarningMonth,
                MostOrdersInDay: dailyOrders?.Count ?? 0,
                MostOrdersDate: dailyOrders?.Date,
                MostOrdersInMonth: monthlyOrders?.Count ?? 0,
                CurrentMonthYear: monthlyOrders != null ? monthlyOrders.Year * 100 + monthlyOrders.Month : 0,
                BestEfficiencyScore: DashboardConstants.DefaultBestEfficiencyScore
            );
        }
    }
}
