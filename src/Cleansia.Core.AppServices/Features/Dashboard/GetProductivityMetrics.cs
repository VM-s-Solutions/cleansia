#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets productivity metrics for an employee: completed jobs against the monthly target and personal
/// bests. Nothing here grades a cleaner on time — the actual work minutes are kept to calibrate
/// estimates only (owner ruling 2026-09-28).
/// </summary>
public class GetProductivityMetrics
{
    public class Query : IQuery<ProductivityMetricsDto>
    {
        public string? EmployeeId { get; init; }
    }

    // public (not internal) — the handler tests construct it directly. See GetOrderAnalytics note (#26).
    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider,
        ICurrencyResolutionService currencyResolutionService)
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

            var completionPercentage = CalculateCompletionPercentage(ordersCompleted, DashboardConstants.DefaultMonthlyOrdersTarget);
            var currency = await currencyResolutionService.ResolveCurrencyForEmployeeAsync(employeeId, cancellationToken);
            var personalBests = await CalculatePersonalBestsAsync(employeeId, currency.Id, cancellationToken);

            return new ProductivityMetricsDto(
                OrdersCompleted: ordersCompleted,
                OrdersTarget: DashboardConstants.DefaultMonthlyOrdersTarget,
                CompletionPercentage: completionPercentage,
                EfficiencyScore: completionPercentage,
                PersonalBests: personalBests
            );
        }

        private static double CalculateCompletionPercentage(int ordersCompleted, int ordersTarget)
        {
            return ordersTarget > 0 ? (double)ordersCompleted / ordersTarget * 100 : 0;
        }

        private async Task<PersonalBests> CalculatePersonalBestsAsync(string employeeId, string currencyId, CancellationToken cancellationToken)
        {
            var allTimeEnd = DateTime.UtcNow;
            var allOrders = await orderRepository
                .GetCompletedOrdersByDateRangeAsync(employeeId, DashboardConstants.AllTimeStartDate, allTimeEnd, cancellationToken);

            var invoices = await employeeInvoiceRepository
                .GetByEmployeeAndDateRangeAsync(employeeId, DashboardConstants.AllTimeStartDate, allTimeEnd, cancellationToken);

            // HighestEarningMonth is rendered with the dashboard's currency code; a month that was
            // "highest" only because it mixed units is not a personal best.
            var highestEarningMonth = invoices
                .Where(i => i.CurrencyId == currencyId)
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

        /// <summary>The best month's completed jobs against the monthly target — the same measure as the month's score.</summary>
        private static double CalculateBestHistoricalEfficiencyScore(IReadOnlyList<Order> allOrders)
        {
            if (allOrders.Count == 0)
                return 0;

            return allOrders
                .GroupBy(o => new { o.CleaningDateTime.Year, o.CleaningDateTime.Month })
                .Max(g => CalculateCompletionPercentage(g.Count(), DashboardConstants.DefaultMonthlyOrdersTarget));
        }
    }
}
