#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public class GetDashboardStats
{
    public record Query(string? EmployeeId = null) : IQuery<DashboardStatsDto>;

    internal class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, DashboardStatsDto>
    {
        public async Task<BusinessResult<DashboardStatsDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            string? employeeId;

            if (role == UserProfile.Administrator.ToString())
            {
                employeeId = query.EmployeeId;
            }
            else
            {
                employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            }

            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<DashboardStatsDto>(new Error(
                    "Employee",
                    BusinessErrorMessage.EmployeeNotFound));
            }

            var today = DateTime.UtcNow;
            var (currentMonthStart, currentMonthEnd) = today.GetCurrentMonthRange();
            var (previousMonthStart, previousMonthEnd) = today.GetPreviousMonthRange();

            var availableOrdersCount = await GetAvailableOrdersCountAsync(employeeId, cancellationToken);
            var activeOrdersCount = await GetActiveOrdersCountAsync(employeeId, cancellationToken);
            var thisMonthCompletedOrders = await GetCompletedOrdersCountAsync(
                employeeId, currentMonthStart, currentMonthEnd, cancellationToken);
            var lastMonthCompletedOrders = await GetCompletedOrdersCountAsync(
                employeeId, previousMonthStart, previousMonthEnd, cancellationToken);
            var latestInvoice = await employeeInvoiceRepository.GetLatestInvoiceAsync(
                employeeId, cancellationToken);

            var pendingEarnings = await orderEmployeePayRepository.SumPendingEarningsAsync(employeeId, cancellationToken);

            return new DashboardStatsDto(
                AvailableOrdersCount: availableOrdersCount,
                MyActiveOrdersCount: activeOrdersCount,
                ThisMonthCompletedOrders: thisMonthCompletedOrders,
                LastMonthCompletedOrders: lastMonthCompletedOrders,
                CurrentPeriodEarnings: pendingEarnings > 0 ? pendingEarnings : latestInvoice?.TotalAmount ?? 0,
                LatestInvoiceStatus: latestInvoice?.Status.ToString()
            );
        }

        private async Task<int> GetAvailableOrdersCountAsync(string excludeEmployeeId, CancellationToken cancellationToken)
        {
            var specification = DashboardSpecifications.CreateAvailableOrdersSpec(excludeEmployeeId);
            return await orderRepository.GetCountAsync(specification.SatisfiedBy(), cancellationToken);
        }

        private async Task<int> GetActiveOrdersCountAsync(string employeeId, CancellationToken cancellationToken)
        {
            var specification = DashboardSpecifications.CreateActiveOrdersSpec(employeeId);
            return await orderRepository.GetCountAsync(specification.SatisfiedBy(), cancellationToken);
        }

        private async Task<int> GetCompletedOrdersCountAsync(string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        {
            var specification = DashboardSpecifications.CreateCompletedOrdersSpec(employeeId, startDate, endDate);
            return await orderRepository.GetCountAsync(specification.SatisfiedBy(), cancellationToken);
        }
    }
}
