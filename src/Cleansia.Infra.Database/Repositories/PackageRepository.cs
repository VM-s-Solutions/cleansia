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

    public async Task<bool> IsInUseAsync(string packageId, CancellationToken cancellationToken)
    {
        if (await Context.OrderPackages.AnyAsync(op => op.PackageId == packageId, cancellationToken))
            return true;

        if (await Context.EmployeePayConfigs.AnyAsync(p => p.PackageId == packageId, cancellationToken))
            return true;

        // A row sitting in a live, server-persisted customer cart is in use: deleting it
        // would silently orphan the cart line — the exact cascade-orphan this guard prevents.
        if (await Context.CartPackageItems.AnyAsync(cpi => cpi.PackageId == packageId, cancellationToken))
            return true;

        // Follow-up: RecurringBookingTemplate stores package ids inside a JSON column, so it
        // cannot be checked with a typed predicate here. Tracked separately; not in scope.

        return false;
    }
}