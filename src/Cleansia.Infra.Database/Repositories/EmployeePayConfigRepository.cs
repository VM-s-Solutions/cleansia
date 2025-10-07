using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeePayConfigRepository(CleansiaDbContext context) : BaseRepository<EmployeePayConfig>(context), IEmployeePayConfigRepository
{
    public Task<EmployeePayConfig?> GetByServiceIdAsync(string serviceId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Service)
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(c => c.ServiceId == serviceId, cancellationToken);
    }

    public Task<EmployeePayConfig?> GetByPackageIdAsync(string packageId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Package)
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(c => c.PackageId == packageId, cancellationToken);
    }

    public IQueryable<EmployeePayConfig> GetAllConfigs()
    {
        return GetDbSet()
            .Include(c => c.Service)
            .Include(c => c.Package)
            .Include(c => c.Currency);
    }
}
