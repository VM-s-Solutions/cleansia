using Cleansia.Core.Domain.Messaging;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// The durable <see cref="ProcessedMessage"/> repository. Auto-registered by the assembly-scan in
/// <c>RepositoryExtensions</c> (it implements <see cref="IRepository{TEntity,TKey}"/>). Mirrors
/// <see cref="ProcessedStripeEventRepository"/>. The claim-insert + own-commit lives in
/// <c>DbIdempotencyGuard</c>; this repo exposes the standard Add/Commit surface plus the existence
/// pre-check the guard uses to short-circuit redeliveries.
/// </summary>
public class ProcessedMessageRepository(CleansiaDbContext context)
    : BaseRepository<ProcessedMessage>(context), IProcessedMessageRepository
{
    public Task<bool> HasProcessedAsync(string messageKey, CancellationToken cancellationToken)
    {
        // Cross-tenant by design — the claim is written before any tenant override, and ProcessedMessage
        // is intentionally not ITenantEntity (see the entity doc). IgnoreQueryFilters() is a hedge in
        // case a future contributor flips it tenant-scoped. Mirrors ProcessedStripeEventRepository.
        return GetDbSet()
            .IgnoreQueryFilters()
            .AnyAsync(m => m.MessageKey == messageKey, cancellationToken);
    }
}
