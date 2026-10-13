using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Specifications;

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
    ///
    /// <para><paramref name="cleanerCurrencyId"/> is the currency the cleaner is paid in
    /// (<c>ICurrencyResolutionService.ResolveCurrencyForEmployeeAsync</c>); an order priced in any
    /// other is not on their board. → <see cref="OrderVisibility.PayableTo(string?, string?)"/></para>
    ///
    /// <para><paramref name="cashJobsHidden"/> takes cash jobs off the board of a cleaner above the
    /// company's cash float cap (owner ruling 2026-09-28, decision 25).</para>
    /// </summary>
    public static OrderSpecification CreateAvailableOrdersSpec(
        string employeeId, string cleanerCurrencyId, DateTime nowUtc, bool cashJobsHidden = false)
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
            excludeEmployeeId: employeeId,
            offerableOnly: true,
            notHeldFromEmployeeId: employeeId,
            nowUtc: nowUtc,
            cleanerCurrencyId: cleanerCurrencyId,
            hideCashFromEmployeeId: cashJobsHidden ? employeeId : null
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
