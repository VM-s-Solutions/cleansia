#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using System.Globalization;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets order analytics for an employee within a specified date range.
/// Provides order distribution by status, trends, and service type breakdown.
/// </summary>
public class GetOrderAnalytics
{
    public class Query : IQuery<OrderAnalyticsDto>
    {
        public string? EmployeeId { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
    }

    // NOTE: public (not internal) because the handler tests construct it directly. Diverges from the
    // GetEarningsAnalytics sibling's `internal` — a harmless inconsistency (MediatR resolves either);
    // unifying to internal would break the direct-construction tests, so the test-driven `public` wins.
    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Query, BusinessResult<OrderAnalyticsDto>>
    {
        public async Task<BusinessResult<OrderAnalyticsDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // S1/S3 (ADR-0001 [OWN-DATA]): never trust the client-supplied EmployeeId. Admins keep
            // oversight over the supplied id; everyone else is scoped to their own resolved employee
            // id so a partner cannot read another cleaner's analytics by passing a foreign id.
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
                return BusinessResult.Failure<OrderAnalyticsDto>(new Error(
                    "Employee",
                    BusinessErrorMessage.EmployeeNotFound));
            }

            var orders = await orderRepository
                .GetEmployeeOrdersByDateRangeAsync(employeeId, request.StartDate, request.EndDate, cancellationToken);

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
