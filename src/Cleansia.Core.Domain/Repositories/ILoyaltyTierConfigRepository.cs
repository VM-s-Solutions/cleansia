using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface ILoyaltyTierConfigRepository : IRepository<LoyaltyTierConfig, string>
{
    /// <summary>
    /// Returns all tier configs for the current tenant (tenant scope is
    /// applied via the global query filter).
    /// </summary>
    Task<IReadOnlyList<LoyaltyTierConfig>> GetAllForTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the config for the given tier within the current tenant scope.
    /// </summary>
    Task<LoyaltyTierConfig?> GetByTierAsync(LoyaltyTier tier, CancellationToken cancellationToken);
}
