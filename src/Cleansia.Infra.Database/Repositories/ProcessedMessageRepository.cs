using Cleansia.Core.Domain.Messaging;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// The durable <see cref="ProcessedMessage"/> repository. Auto-registered by the assembly-scan in
/// <c>RepositoryExtensions</c> (it implements <see cref="IRepository{TEntity,TKey}"/>). Mirrors
/// <see cref="ProcessedStripeEventRepository"/>. The claim-insert + own-commit lives in
/// <c>DbIdempotencyGuard</c>; this repo only exposes the standard Add/Commit surface from the base.
/// </summary>
public class ProcessedMessageRepository(CleansiaDbContext context)
    : BaseRepository<ProcessedMessage>(context), IProcessedMessageRepository
{
}
