using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeePayConfigRepository : IRepository<EmployeePayConfig, string>
{
    Task<EmployeePayConfig?> GetByServiceIdAsync(string serviceId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByPackageIdAsync(string packageId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByEmployeeServiceIdAsync(string employeeId, string serviceId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByEmployeePackageIdAsync(string employeeId, string packageId, CancellationToken cancellationToken);

    /// <summary>
    /// All pay configs scoped to one employee (both employee-specific and
    /// global-fallback rows that apply to them). Used by the admin bulk-edit
    /// pay-config screen.
    /// </summary>
    Task<IReadOnlyList<EmployeePayConfig>> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Service-keyed pay configs whose ServiceId is in the given set, where
    /// the row is either global (EmployeeId == null) or scoped to the given
    /// employee. Used by CalculateOrderPay's lookup pipeline.
    /// </summary>
    Task<IReadOnlyList<EmployeePayConfig>> GetServiceConfigsForOrderAsync(
        IEnumerable<string> serviceIds, string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Package-keyed analogue of <see cref="GetServiceConfigsForOrderAsync"/>.
    /// </summary>
    Task<IReadOnlyList<EmployeePayConfig>> GetPackageConfigsForOrderAsync(
        IEnumerable<string> packageIds, string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// True iff at least one service-or-package config covers the given
    /// employee+order combination. CalculateOrderPay uses this as a guard
    /// before attempting full pay calc.
    /// </summary>
    Task<bool> HasConfigForOrderAsync(
        IEnumerable<string> serviceIds,
        IEnumerable<string> packageIds,
        string employeeId,
        CancellationToken cancellationToken);
}
