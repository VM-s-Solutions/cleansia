using Cleansia.Core.Domain.DeadLettering;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Persistence for <see cref="DeadLetter"/> — the durable dead-letter rows written by the
/// <c>&lt;queue&gt;-poison</c> consumers (ADR-0002 D3 / F3) and read by the admin recovery/replay views.
/// </summary>
public interface IDeadLetterRepository : IRepository<DeadLetter, string>
{
}
