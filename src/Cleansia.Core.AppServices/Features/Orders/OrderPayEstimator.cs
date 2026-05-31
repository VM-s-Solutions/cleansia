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
        IReadOnlyList<EmployeePayConfig> packageConfigs)
    {
        var orderServiceIds = order.SelectedServices.Select(s => s.ServiceId).ToHashSet();
        var orderPackageIds = order.SelectedPackages.Select(p => p.PackageId).ToHashSet();

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

        var (_, _, _, totalPay, _) = allConfigs.CalculateAggregatedPay(order);
        return totalPay;
    }
}
