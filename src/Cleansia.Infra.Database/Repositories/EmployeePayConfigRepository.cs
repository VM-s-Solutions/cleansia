using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeePayConfigRepository(CleansiaDbContext context) : BaseRepository<EmployeePayConfig>(context), IEmployeePayConfigRepository
{
    public Task<EmployeePayConfig?> GetByServiceIdAsync(
        string serviceId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Service)
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(
                c => c.ServiceId == serviceId && c.EmployeeId == null && c.CurrencyId == currencyId,
                cancellationToken);
    }

    public Task<EmployeePayConfig?> GetByPackageIdAsync(
        string packageId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Package)
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(
                c => c.PackageId == packageId && c.EmployeeId == null && c.CurrencyId == currencyId,
                cancellationToken);
    }

    public Task<EmployeePayConfig?> GetByEmployeeServiceIdAsync(
        string employeeId, string serviceId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Service)
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(
                c => c.EmployeeId == employeeId && c.ServiceId == serviceId && c.CurrencyId == currencyId,
                cancellationToken);
    }

    public Task<EmployeePayConfig?> GetByEmployeePackageIdAsync(
        string employeeId, string packageId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Package)
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(
                c => c.EmployeeId == employeeId && c.PackageId == packageId && c.CurrencyId == currencyId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeePayConfig>> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(c => c.EmployeeId == employeeId)
            .Include(c => c.Service)
            .Include(c => c.Package)
            .Include(c => c.Currency)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeePayConfig>> GetServiceConfigsForOrderAsync(
        IEnumerable<string> serviceIds,
        string employeeId,
        IReadOnlyCollection<string> currencyIds,
        CancellationToken cancellationToken)
    {
        var ids = serviceIds.Distinct().ToList();
        return await GetDbSet()
            .Include(c => c.Service)
            .Include(c => c.Package)
            .Include(c => c.Currency)
            .Where(c => c.ServiceId != null && ids.Contains(c.ServiceId)
                && currencyIds.Contains(c.CurrencyId)
                && (c.EmployeeId == null || c.EmployeeId == employeeId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeePayConfig>> GetPackageConfigsForOrderAsync(
        IEnumerable<string> packageIds,
        string employeeId,
        IReadOnlyCollection<string> currencyIds,
        CancellationToken cancellationToken)
    {
        var ids = packageIds.Distinct().ToList();
        return await GetDbSet()
            .Include(c => c.Service)
            .Include(c => c.Package)
            .Include(c => c.Currency)
            .Where(c => c.PackageId != null && ids.Contains(c.PackageId)
                && currencyIds.Contains(c.CurrencyId)
                && (c.EmployeeId == null || c.EmployeeId == employeeId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasConfigForOrderAsync(
        IEnumerable<string> serviceIds,
        IEnumerable<string> packageIds,
        string employeeId,
        IReadOnlyCollection<string> currencyIds,
        CancellationToken cancellationToken)
    {
        var sIds = serviceIds.Distinct().ToList();
        var pIds = packageIds.Distinct().ToList();
        return await GetDbSet()
            .AnyAsync(c =>
                currencyIds.Contains(c.CurrencyId) &&
                (c.EmployeeId == null || c.EmployeeId == employeeId) &&
                ((c.ServiceId != null && sIds.Contains(c.ServiceId)) ||
                 (c.PackageId != null && pIds.Contains(c.PackageId))),
                cancellationToken);
    }
}
