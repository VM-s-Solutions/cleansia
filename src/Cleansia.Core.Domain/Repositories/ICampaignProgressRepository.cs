using CampaignProgressEntity = Cleansia.Core.Domain.Messaging.CampaignProgress;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Persistence for the durable per-campaign resume marker
/// (<see cref="Messaging.CampaignProgress"/>). Used by the durable campaign-progress store to read +
/// upsert the cursor in its own transaction.
/// </summary>
public interface ICampaignProgressRepository : IRepository<CampaignProgressEntity, string>
{
    /// <summary>
    /// The marker row for <paramref name="campaignId"/>, or <c>null</c> if none exists yet. Tenant-global
    /// (the entity is not <see cref="SeedWork.ITenantEntity"/>); ignores query filters as a belt-and-braces
    /// hedge against a future contributor flipping the entity to tenant-scoped.
    /// </summary>
    Task<CampaignProgressEntity?> GetByCampaignIdAsync(string campaignId, CancellationToken cancellationToken);
}
