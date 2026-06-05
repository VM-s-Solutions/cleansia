using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// ADR-0002 D3 (F3) — the durable <see cref="DeadLetter"/> repository. Auto-registered by the
/// assembly-scan in <c>RepositoryExtensions</c> (it implements <see cref="IRepository{TEntity,TKey}"/>).
/// </summary>
public class DeadLetterRepository(CleansiaDbContext context)
    : BaseRepository<DeadLetter>(context), IDeadLetterRepository
{
}
