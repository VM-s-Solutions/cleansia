using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ServiceRepository(CleansiaDbContext context) : BaseRepository<Service>(context), IServiceRepository
{
    public async Task<bool> IsInUseAsync(string serviceId, CancellationToken cancellationToken)
    {
        if (await Context.OrderServices.AnyAsync(os => os.ServiceId == serviceId, cancellationToken))
            return true;

        if (await Context.PackageServices.AnyAsync(ps => ps.ServiceId == serviceId, cancellationToken))
            return true;

        if (await Context.EmployeePayConfigs.AnyAsync(p => p.ServiceId == serviceId, cancellationToken))
            return true;

        // A row sitting in a live, server-persisted customer cart is in use: deleting it
        // would silently orphan the cart line — the exact cascade-orphan this guard prevents.
        if (await Context.CartServiceItems.AnyAsync(csi => csi.ServiceId == serviceId, cancellationToken))
            return true;

        // Follow-up: RecurringBookingTemplate stores service ids inside a JSON column, so it
        // cannot be checked with a typed predicate here. Tracked separately; not in scope.

        return false;
    }
}
