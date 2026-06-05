using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Infra.Database;

/// <summary>
/// ADR-0002 D3 (F3) — the Wave-0 durable backing for <see cref="IDeadLetterStore"/>. Persists a
/// <see cref="DeadLetter"/> row and OWNS ITS OWN COMMIT: the <c>&lt;queue&gt;-poison</c> consumer that
/// calls this has no MediatR pipeline / <c>UnitOfWork</c> behavior wrapping it, so unlike a command
/// handler it must commit explicitly here.
///
/// <para>The row carries the source queue + raw body (+ optional error); <c>TenantId</c> may be
/// <c>null</c> (a poison body can be unparseable) and is left for <c>CleansiaDbContext.CommitAsync</c>
/// to stamp from the ambient tenant when one is in context — the poison consumer does NOT derive a
/// tenant from the body.</para>
/// </summary>
public class DeadLetterStore(IDeadLetterRepository repository) : IDeadLetterStore
{
    public async Task RecordAsync(
        string sourceQueue, string body, string? error = null, CancellationToken ct = default)
    {
        repository.Add(DeadLetter.Create(sourceQueue, body, error));
        await repository.CommitAsync(ct);
    }
}
