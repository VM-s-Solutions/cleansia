using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Core.Domain.Repositories;

public interface ITenantRepository : IRepository<Tenant, string>
{
    /// <summary>
    /// Every operating company in the registry, deactivated ones included. What a deactivated company
    /// means is an open question for the owner (ADR-0061 O-3); until it is answered the default there
    /// applies — <c>IsActive</c> gates nothing — so a job that works through the companies one by one
    /// sweeps a deactivated one like a live one.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllIdsAsync(CancellationToken cancellationToken);
}
