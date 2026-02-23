#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public class GetDashboardStats
{
    public record Query(string? EmployeeId = null) : IQuery<DashboardStatsDto>;

    internal class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, DashboardStatsDto>
    {
        public async Task<BusinessResult<DashboardStatsDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employeeId = query.EmployeeId;

            // If employeeId is not provided, resolve from JWT session
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                var userEmail = userSessionProvider.GetUserEmail();
                var employee = await employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);
                if (employee is null)
                {
                    return BusinessResult.Failure<DashboardStatsDto>(new Error(
                        "Employee",
                        BusinessErrorMessage.EmployeeNotFound));
                }
                employeeId = employee.Id;
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

            return new DashboardStatsDto(
                AvailableOrdersCount: availableOrdersCount,
                MyActiveOrdersCount: activeOrdersCount,
                ThisMonthCompletedOrders: thisMonthCompletedOrders,
                LastMonthCompletedOrders: lastMonthCompletedOrders,
                CurrentPeriodEarnings: latestInvoice?.TotalAmount ?? 0,
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
