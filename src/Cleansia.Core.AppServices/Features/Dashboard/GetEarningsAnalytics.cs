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
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public class GetEarningsAnalytics
{
    public record Query(
        string? EmployeeId,
        DateTime StartDate,
        DateTime EndDate) : IQuery<EarningsAnalyticsDto>;

    internal class Handler(
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Query, BusinessResult<EarningsAnalyticsDto>>
    {
        public async Task<BusinessResult<EarningsAnalyticsDto>> Handle(Query request, CancellationToken cancellationToken)
        {
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
                return BusinessResult.Failure<EarningsAnalyticsDto>(new Error(
                    "Employee",
                    BusinessErrorMessage.EmployeeNotFound));
            }

            var invoices = await employeeInvoiceRepository
                .GetByEmployeeAndDateRangeAsync(employeeId, request.StartDate, request.EndDate, cancellationToken);

            var monthlyEarnings = invoices
                .GroupBy(i => (i.GeneratedAt.Year, i.GeneratedAt.Month))
                .Select(g => g.MapToMonthlyEarning())
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            var totalEarnings = invoices.Sum(i => i.TotalAmount);
            var averageMonthlyEarnings = CalculateAverageMonthlyEarnings(totalEarnings, monthlyEarnings.Count);
            var highestMonth = monthlyEarnings.MaxBy(m => m.Amount);
            var lowestMonth = monthlyEarnings.MinBy(m => m.Amount);
            var growthPercentage = CalculateGrowthPercentage(monthlyEarnings);
            var serviceBreakdown = new Dictionary<string, decimal>();
            var breakdown = invoices.MapToEarningsBreakdown(serviceBreakdown);

            return new EarningsAnalyticsDto(
                MonthlyEarnings: monthlyEarnings,
                Breakdown: breakdown,
                TotalEarnings: totalEarnings,
                AverageMonthlyEarnings: averageMonthlyEarnings,
                HighestMonth: highestMonth,
                LowestMonth: lowestMonth,
                GrowthPercentage: growthPercentage
            );
        }

        private static decimal CalculateAverageMonthlyEarnings(decimal totalEarnings, int monthCount)
        {
            return monthCount > 0 ? totalEarnings / monthCount : 0;
        }

        private static decimal CalculateGrowthPercentage(List<MonthlyEarning> monthlyEarnings)
        {
            if (monthlyEarnings.Count < 2)
                return 0;

            var lastMonth = monthlyEarnings[^1];
            var previousMonth = monthlyEarnings[^2];

            return previousMonth.Amount > 0
                ? ((lastMonth.Amount - previousMonth.Amount) / previousMonth.Amount) * 100
                : 0;
        }
    }
}
