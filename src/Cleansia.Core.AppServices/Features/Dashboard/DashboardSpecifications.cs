using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Specifications;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public static class DashboardSpecifications
{
    public static OrderSpecification CreateAvailableOrdersSpec(string excludeEmployeeId)
    {
        return OrderSpecification.Create(
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
            excludeEmployeeId: excludeEmployeeId
        );
    }

    public static OrderSpecification CreateActiveOrdersSpec(string employeeId)
    {
        return OrderSpecification.Create(
            id: null,
            isActive: null,
            customerName: null,
            customerEmail: null,
            customerPhone: null,
            displayOrderNumber: null,
            employeeId: employeeId,
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
    }

    public static OrderSpecification CreateCompletedOrdersSpec(
        string employeeId,
        DateTime startDate,
        DateTime endDate)
    {
        return OrderSpecification.Create(
            id: null,
            isActive: null,
            customerName: null,
            customerEmail: null,
            customerPhone: null,
            displayOrderNumber: null,
            employeeId: employeeId,
            cleaningDateFrom: startDate,
            cleaningDateTo: endDate,
            paymentStatuses: null,
            paymentTypes: null,
            minTotalPrice: null,
            maxTotalPrice: null,
            orderStatuses: new[] { OrderStatus.Completed },
            hasAvailableSpots: null,
            isUnassigned: null,
            excludeEmployeeId: null
        );
    }
}
