using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public static class DashboardSpecifications
{
    /// <summary>
    /// The orders a cleaner may take. Both terms come from <see cref="OrderAvailability"/> — no status
    /// set can express the rule, because it is payment-qualified.
    ///
    /// <para><b><paramref name="employeeId"/> is spent under TWO OPPOSITE polarities</b>:
    /// <c>excludeEmployeeId</c> drops orders they are already on, <c>notHeldFromEmployeeId</c> keeps the
    /// ones held FOR them. Same id, opposite meanings — neither may be folded into the other.
    /// → /domain/offerability</para>
    /// </summary>
    public static OrderSpecification CreateAvailableOrdersSpec(string employeeId, DateTime nowUtc)
    {
        return OrderSpecification.Create(
            id: null,
            isActive: null,
            customerName: null,
            customerEmail: null,
            customerPhone: null,
            displayOrderNumber: null,
            employeeId: null,
            // The board now admits STARTED jobs so a half-crewed order stays fillable, and nothing
            // ever leaves InProgress except a cleaner tapping complete. Without this floor one job
            // somebody started and abandoned would sit in every cleaner's count forever. Same bound
            // GetPagedOrders puts on the same pane — which had no floor here at all, so the count and
            // the list it belongs to already disagreed. → BookingPolicy.BoardBacklogHours
            cleaningDateFrom: nowUtc.AddHours(-BookingPolicy.BoardBacklogHours),
            cleaningDateTo: null,
            paymentStatuses: null,
            paymentTypes: null,
            minTotalPrice: null,
            maxTotalPrice: null,
            orderStatuses: OrderAvailability.OfferableStatuses,
            hasAvailableSpots: true,
            isUnassigned: null,
            excludeEmployeeId: employeeId,
            offerableOnly: true,
            notHeldFromEmployeeId: employeeId,
            nowUtc: nowUtc
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
