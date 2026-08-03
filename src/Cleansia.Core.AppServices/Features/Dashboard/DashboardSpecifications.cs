using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Specifications;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public static class DashboardSpecifications
{
    /// <summary>
    /// The orders a cleaner may take — the dashboard hero count and the preview beneath it.
    /// Both terms come from <see cref="OrderAvailability"/>: the coarse status set is the
    /// index-served prefilter (and the fail-closed exclusion of pre-backfill NULL rows), and
    /// <c>offerableOnly</c> is the rule itself, which no status set can express because it is
    /// payment-qualified. The set this used to carry was {Pending, Confirmed}, and Pending has no
    /// writer — so the count was structurally zero for an entire pipeline of untaken cash orders
    /// while the Available pane beside it listed them.
    /// </summary>
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
            orderStatuses: OrderAvailability.OfferableStatuses,
            hasAvailableSpots: true,
            isUnassigned: null,
            excludeEmployeeId: excludeEmployeeId,
            offerableOnly: true
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
