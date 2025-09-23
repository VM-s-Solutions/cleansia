using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class PackageRepository(CleansiaDbContext context) : BaseRepository<Package>(context), IPackageRepository
{
    public override Task<Package?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(p => p.IncludedServices)
                .ThenInclude(ps => ps.Service)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}