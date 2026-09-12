using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ExtraRepository(CleansiaDbContext context) : BaseRepository<Extra>(context), IExtraRepository
{
    // The only catalogue-side reference to an Extra is an order line (OrderExtras.ExtraId, ON DELETE
    // RESTRICT). ExtraPrices is Cascade and is not a use. Carts and recurring templates carry services
    // and packages only. OrderExtra derives BaseEntity, so no tenant filter hides another tenant's line.
    public virtual async Task<bool> IsInUseAsync(string extraId, CancellationToken cancellationToken) =>
        await Context.OrderExtras.AnyAsync(oe => oe.ExtraId == extraId, cancellationToken);
}
