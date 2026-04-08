using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeePayConfigRepository : IRepository<EmployeePayConfig, string>
{
    Task<EmployeePayConfig?> GetByServiceIdAsync(string serviceId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByPackageIdAsync(string packageId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByEmployeeServiceIdAsync(string employeeId, string serviceId, CancellationToken cancellationToken);

    Task<EmployeePayConfig?> GetByEmployeePackageIdAsync(string employeeId, string packageId, CancellationToken cancellationToken);

    IQueryable<EmployeePayConfig> GetAllConfigs();

    IQueryable<EmployeePayConfig> GetByEmployeeId(string employeeId);
}
