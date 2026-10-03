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
    /// Returns what the given employee would earn on one seat of the given order
    /// based on their per-employee pay configs: the seat's share of the job, raised by the dirtiness
    /// rate the order was booked at, as <c>CalculateOrderPay</c> writes it for every seat but the first,
    /// which also takes the cent residue. Falls back to default
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
            order.RequiredEmployees,
            order.DirtinessRate,
            order.CurrencyId,
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
            order.RequiredEmployees,
            order.DirtinessRate,
            order.CurrencyId,
            employeeId,
            serviceConfigs,
            packageConfigs);

    /// <summary>
    /// The primitive form both overloads above funnel into, public because a caller with a lean
    /// projection has neither an <c>Order</c> nor an <c>OrderListRow</c> to hand — the dashboard's
    /// available-jobs preview selects six columns and would otherwise have to materialise an aggregate
    /// it does not want, or grow a second copy of this arithmetic. Same math, still one implementation.
    ///
    /// <para><paramref name="orderCurrencyId"/> is what keeps a pay estimate denominated. The configs
    /// arrive from a read scoped to a SET of currencies — the paged list batches a whole page into one
    /// query and a page can span them — so this is the only place that knows which of them belongs to
    /// THIS order. Without it the group-by would fold a cleaner's CZK and EUR rates for one service
    /// into a single group and pick between them with no ORDER BY.</para>
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
        int requiredEmployees,
        decimal dirtinessRate,
        string orderCurrencyId,
        string employeeId,
        IReadOnlyList<EmployeePayConfig> serviceConfigs,
        IReadOnlyList<EmployeePayConfig> packageConfigs) =>
        JobPay(orderServiceIds, orderPackageIds, rooms, bathrooms, orderCurrencyId, employeeId, serviceConfigs, packageConfigs) is { } jobPay
            ? SeatReward(jobPay, dirtinessRate, requiredEmployees)
            : null;

    /// <summary>
    /// The reward the employee's own seat was contracted at, from the figures frozen on it when its contract
    /// for work formed. Null when they hold no seat or its contract has not formed, and the caller quotes the
    /// live estimate instead.
    /// </summary>
    public static decimal? ContractReward(Order order, string employeeId) =>
        order.AssignedEmployees
            .FirstOrDefault(ae => ae.EmployeeId == employeeId)
            ?.FrozenPay(order.DirtinessRate, order.RequiredEmployees, firstSeat: false)
            ?.totalPay;

    /// <summary>Projection-row twin of the entity overload, for the list handler.</summary>
    public static decimal? ContractReward(OrderListRow order, string employeeId) =>
        order.AssignedEmployees.FirstOrDefault(ae => ae.EmployeeId == employeeId)
            is { JobBasePay: { } jobBasePay, JobExtrasPay: { } jobExtrasPay, JobMinPay: { } jobMinPay, JobMaxPay: { } jobMaxPay }
            ? SeatReward((jobBasePay, jobExtrasPay, jobMinPay, jobMaxPay), order.DirtinessRate, order.RequiredEmployees)
            : null;

    /// <summary>
    /// One seat's reward on a job priced at <paramref name="jobPay"/>, as the board and the contract for work
    /// state it: the share of every seat but the first, which is paid the cent residue on top.
    /// </summary>
    internal static decimal SeatReward(
        (decimal jobBasePay, decimal jobExtrasPay, decimal jobMinPay, decimal jobMaxPay) jobPay,
        decimal dirtinessRate,
        int requiredEmployees) =>
        PayCalculatorExtensions.CalculateSeatPay(
            jobPay.jobBasePay, jobPay.jobExtrasPay, jobPay.jobMinPay, jobPay.jobMaxPay,
            dirtinessRate, requiredEmployees, firstSeat: false).totalPay;

    /// <summary>
    /// The four job figures the employee's rates in the order's currency price the order at, their own
    /// override before the platform-wide row; what a seat freezes when its contract for work forms. Null when
    /// no rate covers any of the order's services or packages.
    /// </summary>
    internal static (decimal jobBasePay, decimal jobExtrasPay, decimal jobMinPay, decimal jobMaxPay)? JobPay(
        HashSet<string> orderServiceIds,
        HashSet<string> orderPackageIds,
        int rooms,
        int bathrooms,
        string orderCurrencyId,
        string employeeId,
        IReadOnlyList<EmployeePayConfig> serviceConfigs,
        IReadOnlyList<EmployeePayConfig> packageConfigs)
    {
        var matchedServiceConfigs = serviceConfigs
            .Where(c => c.ServiceId != null && orderServiceIds.Contains(c.ServiceId))
            .Where(c => c.CurrencyId == orderCurrencyId)
            .GroupBy(c => c.ServiceId)
            .Select(g => g.FirstOrDefault(c => c.EmployeeId == employeeId) ?? g.First());

        var matchedPackageConfigs = packageConfigs
            .Where(c => c.PackageId != null && orderPackageIds.Contains(c.PackageId))
            .Where(c => c.CurrencyId == orderCurrencyId)
            .GroupBy(c => c.PackageId)
            .Select(g => g.FirstOrDefault(c => c.EmployeeId == employeeId) ?? g.First());

        var allConfigs = matchedServiceConfigs.Concat(matchedPackageConfigs).ToList();
        return allConfigs.Count == 0 ? null : allConfigs.AggregateJobPay(rooms, bathrooms);
    }
}
