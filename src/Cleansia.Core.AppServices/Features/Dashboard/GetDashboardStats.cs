#nullable enable
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Specifications;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Gets dashboard statistics for an employee in a single optimized query.
/// Replaces multiple separate API calls with one consolidated endpoint.
/// </summary>
public class GetDashboardStats
{
    public class Query : IRequest<DashboardStatsDto>
    {
        public required string EmployeeId { get; init; }
    }

    internal class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository)
        : IRequestHandler<Query, DashboardStatsDto>
    {
        public async Task<DashboardStatsDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow;
            var startOfMonth = DateTime.SpecifyKind(new DateTime(today.Year, today.Month, 1), DateTimeKind.Utc);
            var endOfMonth = DateTime.SpecifyKind(startOfMonth.AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59), DateTimeKind.Utc);
            var startOfLastMonth = DateTime.SpecifyKind(new DateTime(today.Year, today.Month - 1, 1), DateTimeKind.Utc);
            var endOfLastMonth = DateTime.SpecifyKind(startOfMonth.AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59), DateTimeKind.Utc);

            // Get available orders count (orders with spots, excluding this employee)
            var availableOrdersSpec = OrderSpecification.Create(
                id: null,
                isActive: null,
                customerName: null,
                customerEmail: null,
                customerPhone: null,
                displayOrderNumber: null,
                employeeId: null,
                cleaningDateFrom: null,
                cleaningDateTo: null,
                paymentStatuses: null,
                paymentTypes: null,
                minTotalPrice: null,
                maxTotalPrice: null,
                orderStatuses: new[] { OrderStatus.Pending, OrderStatus.Confirmed },
                hasAvailableSpots: true,
                isUnassigned: null,
                excludeEmployeeId: request.EmployeeId
            );
            var availableOrdersCount = await orderRepository.GetCountAsync(
                availableOrdersSpec.SatisfiedBy(),
                cancellationToken
            );

            // Get active orders count (in progress orders for this employee)
            var activeOrdersSpec = OrderSpecification.Create(
                id: null,
                isActive: null,
                customerName: null,
                customerEmail: null,
                customerPhone: null,
                displayOrderNumber: null,
                employeeId: request.EmployeeId,
                cleaningDateFrom: null,
                cleaningDateTo: null,
                paymentStatuses: null,
                paymentTypes: null,
                minTotalPrice: null,
                maxTotalPrice: null,
                orderStatuses: new[] { OrderStatus.InProgress },
                hasAvailableSpots: null,
                isUnassigned: null,
                excludeEmployeeId: null
            );
            var activeOrdersCount = await orderRepository.GetCountAsync(
                activeOrdersSpec.SatisfiedBy(),
                cancellationToken
            );

            // Get this month completed orders
            var thisMonthSpec = OrderSpecification.Create(
                id: null,
                isActive: null,
                customerName: null,
                customerEmail: null,
                customerPhone: null,
                displayOrderNumber: null,
                employeeId: request.EmployeeId,
                cleaningDateFrom: startOfMonth,
                cleaningDateTo: endOfMonth,
                paymentStatuses: null,
                paymentTypes: null,
                minTotalPrice: null,
                maxTotalPrice: null,
                orderStatuses: new[] { OrderStatus.Completed },
                hasAvailableSpots: null,
                isUnassigned: null,
                excludeEmployeeId: null
            );
            var thisMonthCompletedOrders = await orderRepository.GetCountAsync(
                thisMonthSpec.SatisfiedBy(),
                cancellationToken
            );

            // Get last month completed orders
            var lastMonthSpec = OrderSpecification.Create(
                id: null,
                isActive: null,
                customerName: null,
                customerEmail: null,
                customerPhone: null,
                displayOrderNumber: null,
                employeeId: request.EmployeeId,
                cleaningDateFrom: startOfLastMonth,
                cleaningDateTo: endOfLastMonth,
                paymentStatuses: null,
                paymentTypes: null,
                minTotalPrice: null,
                maxTotalPrice: null,
                orderStatuses: new[] { OrderStatus.Completed },
                hasAvailableSpots: null,
                isUnassigned: null,
                excludeEmployeeId: null
            );
            var lastMonthCompletedOrders = await orderRepository.GetCountAsync(
                lastMonthSpec.SatisfiedBy(),
                cancellationToken
            );

            // Get latest invoice for earnings
            var latestInvoice = await employeeInvoiceRepository.GetLatestInvoiceAsync(
                request.EmployeeId,
                cancellationToken
            );

            return new DashboardStatsDto(
                AvailableOrdersCount: availableOrdersCount,
                MyActiveOrdersCount: activeOrdersCount,
                ThisMonthCompletedOrders: thisMonthCompletedOrders,
                LastMonthCompletedOrders: lastMonthCompletedOrders,
                CurrentPeriodEarnings: latestInvoice?.TotalAmount ?? 0,
                LatestInvoiceStatus: latestInvoice?.Status.ToString()
            );
        }
    }
}
