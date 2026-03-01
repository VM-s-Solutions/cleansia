using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.Domain.Repositories;

public interface IFeatureFlagRepository : IRepository<FeatureFlag, string>
{
    Task<FeatureFlag?> GetByNameAndScopeAsync(string name, string scope, string? scopeValue, CancellationToken cancellationToken);
    Task<bool> ExistsWithNameAndScopeAsync(string name, string scope, string? scopeValue, CancellationToken cancellationToken);
    Task<List<FeatureFlag>> GetByScopeAsync(string scope, string? scopeValue, CancellationToken cancellationToken);
}
