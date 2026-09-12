using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeePayConfigRepository : IRepository<EmployeePayConfig, string>
{
    /// <summary>
    /// The four duplicate probes behind <c>CreatePayConfig</c>. Each takes a CURRENCY, and must:
    /// the unique index is <c>(EmployeeId, ServiceId, PackageId, CurrencyId)</c>, so a rate in a
    /// second currency for the same entry is a legal row and the intended shape.
    ///
    /// <para>Without the currency term these compared two of the index's four columns, which made
    /// them refuse a EUR rate on the strength of the CZK one — and since the seed writes a
    /// platform-wide CZK row for every priced service and package, that refusal was guaranteed for
    /// the very first EUR pay config anyone tried to create.</para>
    /// </summary>
    Task<EmployeePayConfig?> GetByServiceIdAsync(string serviceId, string currencyId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByPackageIdAsync(string packageId, string currencyId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByEmployeeServiceIdAsync(string employeeId, string serviceId, string currencyId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByEmployeePackageIdAsync(string employeeId, string packageId, string currencyId, CancellationToken cancellationToken);

    /// <summary>
    /// All pay configs scoped to one employee (both employee-specific and
    /// global-fallback rows that apply to them). Used by the admin bulk-edit
    /// pay-config screen.
    /// </summary>
    Task<IReadOnlyList<EmployeePayConfig>> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Service-keyed pay configs whose ServiceId is in the given set, IN ONE CURRENCY, where the row is
    /// either global (EmployeeId == null) or scoped to the given employee. Used by CalculateOrderPay's
    /// lookup pipeline.
    ///
    /// <para><paramref name="currencyIds"/> is a SET rather than one currency because one caller
    /// batches a whole page of orders into a single read, and a page can span currencies — narrowing it
    /// to one would have reintroduced the N+1 that batching removed. Single-order callers pass one.
    /// It is a required argument rather than a filter the caller may forget: a pay rate is an amount in
    /// a currency, and once a cleaner can hold a rate per currency an unscoped read hands the estimator
    /// rows denominated in several, which it then picks between with no ORDER BY.</para>
    ///
    /// <para>The per-ORDER narrowing still happens downstream — <c>OrderPayEstimator</c> filters on the
    /// order's own currency, because a set-scoped read cannot know which of a page's orders each row
    /// belongs to.</para>
    /// </summary>
    Task<IReadOnlyList<EmployeePayConfig>> GetServiceConfigsForOrderAsync(
        IEnumerable<string> serviceIds,
        string employeeId,
        IReadOnlyCollection<string> currencyIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Package-keyed analogue of <see cref="GetServiceConfigsForOrderAsync"/>.
    /// </summary>
    Task<IReadOnlyList<EmployeePayConfig>> GetPackageConfigsForOrderAsync(
        IEnumerable<string> packageIds,
        string employeeId,
        IReadOnlyCollection<string> currencyIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// True iff at least one service-or-package config covers the given employee+order combination
    /// IN THE ORDER'S CURRENCY. CalculateOrderPay uses this as a guard before attempting full pay calc.
    ///
    /// <para>The currency term is what makes the guard mean anything: a config in another currency
    /// cannot pay this order, so counting it would let the calculation proceed and then produce a zero
    /// from an empty set.</para>
    /// </summary>
    Task<bool> HasConfigForOrderAsync(
        IEnumerable<string> serviceIds,
        IEnumerable<string> packageIds,
        string employeeId,
        IReadOnlyCollection<string> currencyIds,
        CancellationToken cancellationToken);
}
