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

    public virtual async Task<bool> IsInUseAsync(string packageId, CancellationToken cancellationToken)
    {
        if (await Context.OrderPackages.AnyAsync(op => op.PackageId == packageId, cancellationToken))
            return true;

        if (await Context.EmployeePayConfigs.AnyAsync(p => p.PackageId == packageId, cancellationToken))
            return true;

        // A row sitting in a live, server-persisted customer cart is in use: deleting it
        // would silently orphan the cart line — the exact cascade-orphan this guard prevents.
        if (await Context.CartPackageItems.AnyAsync(cpi => cpi.PackageId == packageId, cancellationToken))
            return true;

        // RecurringBookingTemplate stores package ids inside a JSON array column, which no foreign key
        // can guard — a deleted catalog item would leave a dangling JSON id that materializes into a
        // broken recurring booking. The column has a JSON value converter, so it cannot be filtered in
        // SQL provider-agnostically; the id collections are materialized and checked in memory. This is
        // acceptable: templates are a small, no-UI-yet table and this stays a fast pre-check (no FK can
        // make the DB the arbiter for JSON refs, so the TOCTOU window here is tolerated by design).
        // IgnoreQueryFilters because the catalog row is tenantless platform config — a reference in ANY
        // tenant's template counts, and the admin's tenant claim must not hide other tenants' references.
        if (await CatalogReferenceJson.IsReferencedByTemplatePackageAsync(Context, packageId, cancellationToken))
            return true;

        return false;
    }
}