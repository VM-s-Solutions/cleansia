#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets productivity metrics for an employee.
/// Provides KPIs, targets, and personal best achievements.
/// All metrics are calculated from real order data.
/// </summary>
public class GetProductivityMetrics
{
    public class Query : IQuery<ProductivityMetricsDto>
    {
        public string? EmployeeId { get; init; }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Query, BusinessResult<ProductivityMetricsDto>>
    {
        public async Task<BusinessResult<ProductivityMetricsDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // S1/S3 (ADR-0001 [OWN-DATA]): admins keep oversight over the supplied id; everyone else
            // is scoped to their own resolved employee id. The resolved id is threaded through BOTH
            // the order path AND CalculatePersonalBestsAsync's invoice path so a partner cannot read
            // another cleaner's historical EARNINGS by passing a foreign id.
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            string? employeeId;

            if (role == UserProfile.Administrator.ToString())
            {
                employeeId = request.EmployeeId;
            }
            else
            {
                employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            }

            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<ProductivityMetricsDto>(new Error(
                    "Employee",
                    BusinessErrorMessage.EmployeeNotFound));
            }

            var today = DateTime.UtcNow;
            var (currentMonthStart, currentMonthEnd) = today.GetCurrentMonthRange();

            var completedSpec = DashboardSpecifications.CreateCompletedOrdersSpec(
                employeeId, currentMonthStart, currentMonthEnd);
            var ordersCompleted = await orderRepository.GetCountAsync(
                completedSpec.SatisfiedBy(), cancellationToken);

            var completedOrders = await orderRepository
                .GetCompletedOrdersByDateRangeAsync(employeeId, currentMonthStart, currentMonthEnd, cancellationToken);

            var averageCompletionTimeMinutes = CalculateAverageActualCompletionTime(completedOrders);
            var onTimeCompletionRate = CalculateOnTimeCompletionRate(completedOrders);
            var completionPercentage = CalculateCompletionPercentage(ordersCompleted, DashboardConstants.DefaultMonthlyOrdersTarget);
            var efficiencyScore = CalculateEfficiencyScore(completionPercentage, onTimeCompletionRate);
            var personalBests = await CalculatePersonalBestsAsync(employeeId, cancellationToken);

            return new ProductivityMetricsDto(
                OrdersCompleted: ordersCompleted,
                OrdersTarget: DashboardConstants.DefaultMonthlyOrdersTarget,
                CompletionPercentage: completionPercentage,
                AverageCompletionTimeMinutes: averageCompletionTimeMinutes,
                OnTimeCompletionRate: onTimeCompletionRate,
                EfficiencyScore: efficiencyScore,
                PersonalBests: personalBests
            );
        }

        /// <summary>
        /// Calculates the average actual completion time from orders that have ActualCompletionTime recorded.
        /// Falls back to estimated time if no actual times are available.
        /// </summary>
        private static double CalculateAverageActualCompletionTime(IReadOnlyList<Order> completedOrders)
        {
            if (completedOrders.Count == 0)
                return 0;

            var ordersWithActualTime = completedOrders.Where(o => o.ActualCompletionTime.HasValue).ToList();

            if (ordersWithActualTime.Count > 0)
            {
                return ordersWithActualTime.Average(o => o.ActualCompletionTime!.Value);
            }

            // Fall back to estimated time if no actual completion times are recorded
            return completedOrders.Average(o => o.EstimatedTime);
        }

        /// <summary>
        /// Calculates the percentage of orders completed within or under the estimated time.
        /// An order is "on time" if ActualCompletionTime is less than or equal to EstimatedTime.
        /// </summary>
        private static double CalculateOnTimeCompletionRate(IReadOnlyList<Order> completedOrders)
        {
            if (completedOrders.Count == 0)
                return 100.0; // Default to 100% if no orders

            var ordersWithActualTime = completedOrders.Where(o => o.ActualCompletionTime.HasValue).ToList();

            if (ordersWithActualTime.Count == 0)
                return 100.0; // Default to 100% if no actual times recorded

            var onTimeOrders = ordersWithActualTime.Count(o => o.ActualCompletionTime!.Value <= o.EstimatedTime);
            return (double)onTimeOrders / ordersWithActualTime.Count * 100;
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
                .GetCompletedOrdersByDateRangeAsync(employeeId, DashboardConstants.AllTimeStartDate, allTimeEnd, cancellationToken);

            var invoices = await employeeInvoiceRepository
                .GetByEmployeeAndDateRangeAsync(employeeId, DashboardConstants.AllTimeStartDate, allTimeEnd, cancellationToken);

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

            var bestEfficiencyScore = CalculateBestHistoricalEfficiencyScore(allOrders);

            return new PersonalBests(
                HighestEarningMonth: highestEarningMonth,
                MostOrdersInDay: dailyOrders?.Count ?? 0,
                MostOrdersDate: dailyOrders?.Date,
                MostOrdersInMonth: monthlyOrders?.Count ?? 0,
                CurrentMonthYear: monthlyOrders != null ? monthlyOrders.Year * 100 + monthlyOrders.Month : 0,
                BestEfficiencyScore: bestEfficiencyScore
            );
        }

        /// <summary>
        /// Calculates the best monthly on-time completion rate from all historical orders.
        /// </summary>
        private static double CalculateBestHistoricalEfficiencyScore(IReadOnlyList<Order> allOrders)
        {
            if (allOrders.Count == 0)
                return 0;

            var ordersWithActualTime = allOrders.Where(o => o.ActualCompletionTime.HasValue).ToList();

            if (ordersWithActualTime.Count == 0)
                return 100.0; // Default if no actual times recorded

            var monthlyGroups = ordersWithActualTime
                .GroupBy(o => new { o.CleaningDateTime.Year, o.CleaningDateTime.Month })
                .ToList();

            if (monthlyGroups.Count == 0)
                return 100.0;

            var monthlyOnTimeRates = monthlyGroups.Select(g =>
            {
                var onTimeOrders = g.Count(o => o.ActualCompletionTime!.Value <= o.EstimatedTime);
                return (double)onTimeOrders / g.Count() * 100;
            });

            return monthlyOnTimeRates.Max();
        }
    }
}
