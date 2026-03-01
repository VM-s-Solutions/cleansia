using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class FeatureFlagRepository(CleansiaDbContext context) : BaseRepository<FeatureFlag>(context), IFeatureFlagRepository
{
    public Task<FeatureFlag?> GetByNameAndScopeAsync(string name, string scope, string? scopeValue, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(
            f => f.Name == name && f.Scope == scope && f.ScopeValue == scopeValue,
            cancellationToken);
    }

    public Task<bool> ExistsWithNameAndScopeAsync(string name, string scope, string? scopeValue, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(
            f => f.Name == name && f.Scope == scope && f.ScopeValue == scopeValue,
            cancellationToken);
    }

    public Task<List<FeatureFlag>> GetByScopeAsync(string scope, string? scopeValue, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(f => f.Scope == scope && f.ScopeValue == scopeValue)
            .ToListAsync(cancellationToken);
    }
}
