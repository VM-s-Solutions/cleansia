using System.Collections.Concurrent;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Infra.Azure.Storage.Queues;

/// <summary>
/// In-memory backing for <see cref="ICampaignProgressStore"/> (sibling to
/// <see cref="InMemoryIdempotencyGuard"/>). Registered as a <b>singleton</b> so a campaign's resume
/// cursor survives redeliveries within one worker process — that is what makes a redelivered fan-out
/// resume past the last processed recipient instead of restarting at offset 0.
///
/// <para>The cursor lives only in this process's memory, so it does not survive a worker restart or
/// span scaled-out instances; the accepted residual is a rare re-cost of the whole base under those
/// conditions — never a duplicate <i>effect</i> (the downstream <c>push:{UserId}:{EventKey}</c> dedup
/// remains the effect guard). A durable backing would close that gap with no change to the interface.</para>
/// </summary>
public sealed class InMemoryCampaignProgressStore : ICampaignProgressStore
{
    private readonly ConcurrentDictionary<string, string> _cursorByCampaign = new();
    private readonly ConcurrentDictionary<string, byte> _completeCampaigns = new();

    public Task<CampaignProgress> GetAsync(string campaignId, CancellationToken ct = default) =>
        Task.FromResult(new CampaignProgress(
            _cursorByCampaign.TryGetValue(campaignId, out var cursor) ? cursor : null,
            _completeCampaigns.ContainsKey(campaignId)));

    public Task AdvanceAsync(string campaignId, string lastUserId, CancellationToken ct = default)
    {
        _cursorByCampaign[campaignId] = lastUserId;
        return Task.CompletedTask;
    }

    public Task MarkCompleteAsync(string campaignId, CancellationToken ct = default)
    {
        _completeCampaigns.TryAdd(campaignId, 0);
        return Task.CompletedTask;
    }
}
