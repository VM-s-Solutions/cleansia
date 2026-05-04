using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LoyaltyTierConfigRepository(CleansiaDbContext context)
    : BaseRepository<LoyaltyTierConfig>(context), ILoyaltyTierConfigRepository
{
    public async Task<IReadOnlyList<LoyaltyTierConfig>> GetAllForTenantAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .OrderBy(c => c.LifetimePointsThreshold)
            .ToListAsync(cancellationToken);
    }

    public Task<LoyaltyTierConfig?> GetByTierAsync(LoyaltyTier tier, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(c => c.Tier == tier, cancellationToken);
    }
}
