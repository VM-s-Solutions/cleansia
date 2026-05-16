using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ProcessedStripeEventRepository(CleansiaDbContext context)
    : BaseRepository<ProcessedStripeEvent>(context), IProcessedStripeEventRepository
{
    public Task<bool> HasProcessedAsync(string stripeEventId, CancellationToken cancellationToken)
    {
        // Cross-tenant by design — Stripe webhooks are not tenant-scoped.
        // The entity also doesn't implement ITenantEntity so the global
        // filter never gets applied, but using IgnoreQueryFilters() here is
        // a belt-and-braces hedge against a future contributor flipping the
        // entity to tenant-scoped.
        return GetDbSet()
            .IgnoreQueryFilters()
            .AnyAsync(e => e.StripeEventId == stripeEventId, cancellationToken);
    }
}
