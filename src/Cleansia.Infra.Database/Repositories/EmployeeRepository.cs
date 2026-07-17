using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeRepository(CleansiaDbContext context) : BaseRepository<Employee>(context), IEmployeeRepository
{
    public Task<Employee?> GetByUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(e => e.User)
            .Include(e => e.Address)
            .Include(e => e.Nationality)
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }

    public Task<Employee?> GetByUserEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default)
    {
        // Same by-email match as GetByUserEmailAsync but bypassing the tenant filter, so a
        // tenant-stamped employee still resolves on the tenant-less token-minting paths (T-0361).
        return GetQueryableIgnoringTenant()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }

    public Task<bool> ExistsWithUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(e => e.User)
            .AnyAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }

    public Task<List<Employee>> GetAllActiveWithUserAsync(CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(e => e.User)
                .ThenInclude(u => u.PreferredLanguage)
            .Where(e => e.User != null && e.ContractStatus != Core.Domain.Enums.ContractStatus.Terminated)
            .ToListAsync(cancellationToken);
    }

    public override Task<Employee?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(e => e.User)
            .Include(e => e.Address)
                .ThenInclude(a => a.Country)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public Task<Employee?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
    {
        return GetQueryableIgnoringTenant()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
}