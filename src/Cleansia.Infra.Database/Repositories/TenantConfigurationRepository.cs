using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class TenantConfigurationRepository(CleansiaDbContext context) : BaseRepository<TenantConfiguration>(context), ITenantConfigurationRepository
{
    public Task<TenantConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantConfiguration>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .AsNoTracking()
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken);
    }
}
