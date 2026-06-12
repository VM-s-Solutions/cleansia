using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using CampaignProgressEntity = Cleansia.Core.Domain.Messaging.CampaignProgress;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// The durable <see cref="CampaignProgressEntity"/> repository. Auto-registered by the assembly-scan in
/// <c>RepositoryExtensions</c> (it implements <see cref="IRepository{TEntity,TKey}"/>). The upsert +
/// own-commit lives in <c>DbCampaignProgressStore</c>.
/// </summary>
public class CampaignProgressRepository(CleansiaDbContext context)
    : BaseRepository<CampaignProgressEntity>(context), ICampaignProgressRepository
{
    public Task<CampaignProgressEntity?> GetByCampaignIdAsync(string campaignId, CancellationToken cancellationToken)
    {
        // Tenant-global by design (the entity is not ITenantEntity). IgnoreQueryFilters() is a
        // belt-and-braces hedge against a future contributor flipping it to tenant-scoped — mirrors
        // ProcessedStripeEventRepository.HasProcessedAsync.
        return GetDbSet()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.CampaignId == campaignId, cancellationToken);
    }
}
