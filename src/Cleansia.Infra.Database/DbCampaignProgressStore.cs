using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using CampaignProgressEntity = Cleansia.Core.Domain.Messaging.CampaignProgress;

namespace Cleansia.Infra.Database;

/// <summary>
/// The durable backing for <see cref="ICampaignProgressStore"/> — replaces the process-local
/// <c>InMemoryCampaignProgressStore</c> so a campaign's resume cursor survives a worker restart and
/// spans scaled-out instances. OWNS ITS OWN COMMIT like <see cref="DeadLetterStore"/> (the fan-out
/// consumer has no MediatR <c>UnitOfWork</c>).
///
/// <para>This is a Bucket-C cost/spam layer, NOT an at-most-once effect control — the downstream
/// <c>push:{UserId}:{EventKey}</c> claim (<c>DbIdempotencyGuard</c>) is the effect control. So
/// <see cref="AdvanceAsync"/> / <see cref="MarkCompleteAsync"/> do a simple find-or-insert-then-update
/// upsert; a rare first-advance race surfacing as a 23505 is a benign re-cost, not a duplicate effect.</para>
/// </summary>
public class DbCampaignProgressStore(ICampaignProgressRepository repository) : ICampaignProgressStore
{
    public async Task<CampaignProgress> GetAsync(string campaignId, CancellationToken ct = default)
    {
        var row = await repository.GetByCampaignIdAsync(campaignId, ct);
        return row is null
            ? new CampaignProgress(null, false)
            : new CampaignProgress(row.LastProcessedUserId, row.IsComplete);
    }

    public async Task AdvanceAsync(string campaignId, string lastUserId, CancellationToken ct = default)
    {
        var row = await GetOrCreateAsync(campaignId, ct);
        row.Advance(lastUserId);
        await repository.CommitAsync(ct);
    }

    public async Task MarkCompleteAsync(string campaignId, CancellationToken ct = default)
    {
        var row = await GetOrCreateAsync(campaignId, ct);
        row.MarkComplete();
        await repository.CommitAsync(ct);
    }

    private async Task<CampaignProgressEntity> GetOrCreateAsync(string campaignId, CancellationToken ct)
    {
        var row = await repository.GetByCampaignIdAsync(campaignId, ct);
        if (row is not null)
        {
            return row;
        }

        row = CampaignProgressEntity.Create(campaignId);
        repository.Add(row);
        return row;
    }
}
