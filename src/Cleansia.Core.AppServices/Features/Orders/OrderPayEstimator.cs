using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Pure-function pay estimator shared between the list and detail
/// handlers. Both surfaces show the same number to the cleaner — the
/// list cards anchor the offer pickup decision; the detail hero
/// re-confirms it after they tap through — and they MUST agree, so the
/// estimator lives in one place rather than being copy-pasted.
/// </summary>
internal static class OrderPayEstimator
{
    /// <summary>
    /// Returns what the given employee would earn for the given order
    /// based on their per-employee pay configs. Falls back to default
    /// configs when no per-employee override exists. Returns null when
    /// no config matches any of the order's services / packages — the
    /// caller treats that as "we can't quote pay, hide the chip".
    /// </summary>
    public static decimal? Estimate(
        Order order,
        string employeeId,
        IReadOnlyList<EmployeePayConfig> serviceConfigs,
        IReadOnlyList<EmployeePayConfig> packageConfigs) =>
        Estimate(
            order.SelectedServices.Select(s => s.ServiceId).ToHashSet(),
            order.SelectedPackages.Select(p => p.PackageId).ToHashSet(),
            order.Rooms,
            order.Bathrooms,
            order.TravelDistance,
            employeeId,
            serviceConfigs,
            packageConfigs);

    /// <summary>
    /// Projection-row twin of the entity overload for the list handler, which no longer
    /// materializes Order entities. Same math, one implementation.
    /// </summary>
    public static decimal? Estimate(
        OrderListRow order,
        string employeeId,
        IReadOnlyList<EmployeePayConfig> serviceConfigs,
        IReadOnlyList<EmployeePayConfig> packageConfigs) =>
        Estimate(
            order.SelectedServices.Select(s => s.Id).ToHashSet(),
            order.SelectedPackages.Select(p => p.Id).ToHashSet(),
            order.Rooms,
            order.Bathrooms,
            order.TravelDistance,
            employeeId,
            serviceConfigs,
            packageConfigs);

    /// <summary>
    /// The primitive form both overloads above funnel into, public because a caller with a lean
    /// projection has neither an <c>Order</c> nor an <c>OrderListRow</c> to hand — the dashboard's
    /// available-jobs preview selects six columns and would otherwise have to materialise an aggregate
    /// it does not want, or grow a second copy of this arithmetic. Same math, still one implementation.
    ///
    /// <para><c>internal</c>, not <c>public</c>: the only caller outside this file is in the same
    /// assembly, and the enclosing type is internal anyway. It also keeps
    /// <c>PayCoverageEstimatorAgreementTests</c>'s reflection lookup working — that test reaches this
    /// overload with <c>BindingFlags.NonPublic</c>, which still finds an internal method.</para>
    /// </summary>
    internal static decimal? Estimate(
        HashSet<string> orderServiceIds,
        HashSet<string> orderPackageIds,
        int rooms,
        int bathrooms,
        decimal? travelDistance,
        string employeeId,
        IReadOnlyList<EmployeePayConfig> serviceConfigs,
        IReadOnlyList<EmployeePayConfig> packageConfigs)
    {
        var matchedServiceConfigs = serviceConfigs
            .Where(c => c.ServiceId != null && orderServiceIds.Contains(c.ServiceId))
            .GroupBy(c => c.ServiceId)
            .Select(g => g.FirstOrDefault(c => c.EmployeeId == employeeId) ?? g.First());

        var matchedPackageConfigs = packageConfigs
            .Where(c => c.PackageId != null && orderPackageIds.Contains(c.PackageId))
            .GroupBy(c => c.PackageId)
            .Select(g => g.FirstOrDefault(c => c.EmployeeId == employeeId) ?? g.First());

        var allConfigs = matchedServiceConfigs.Concat(matchedPackageConfigs).ToList();
        if (allConfigs.Count == 0)
        {
            return null;
        }

        var (_, _, _, totalPay, _) = allConfigs.CalculateAggregatedPay(rooms, bathrooms, travelDistance);
        return totalPay;
    }
}
