using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class TenantRepository(CleansiaDbContext context) : BaseRepository<Tenant>(context), ITenantRepository
{
    public async Task<IReadOnlyList<string>> GetAllIdsAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }
}
