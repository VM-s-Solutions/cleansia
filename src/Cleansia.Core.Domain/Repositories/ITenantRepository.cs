using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Core.Domain.Repositories;

public interface ITenantRepository : IRepository<Tenant, string>
{
    /// <summary>
    /// Every operating company in the registry, deactivated ones included: a job that works through
    /// the companies one by one owes a deactivated company its sweeps as much as a live one.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllIdsAsync(CancellationToken cancellationToken);
}
