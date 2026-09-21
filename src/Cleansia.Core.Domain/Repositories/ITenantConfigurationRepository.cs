using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.Domain.Repositories;

public interface ITenantConfigurationRepository : IRepository<TenantConfiguration, string>
{
    Task<TenantConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantConfiguration>> GetAllAsync(CancellationToken cancellationToken);
}
