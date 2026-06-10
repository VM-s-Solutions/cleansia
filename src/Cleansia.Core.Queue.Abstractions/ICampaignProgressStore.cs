namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// Per-campaign resume marker for the sitewide-promo fan-out consumer (ADR-0002 D2.3 — the named
/// nice-to-have, a Bucket-C cost/spam layer on top of the load-bearing downstream
/// <c>push:{UserId}:{EventKey}</c> effect dedup, which this never replaces).
///
/// <para>The consumer pages opted-in users in a stable <c>OrderBy(UserId)</c> order. After each page it
/// <see cref="AdvanceAsync"/>es the cursor to the last fully-processed <c>UserId</c>; on redelivery it
/// reads <see cref="GetAsync"/> and seeks past that cursor instead of restarting at offset 0, so a flaky
/// page read does not re-enqueue (re-cost) the recipients already processed. <see cref="MarkCompleteAsync"/>
/// makes a redelivery of a finished campaign a no-op.</para>
///
/// <para>The guarantee is process-local (sibling to <see cref="IIdempotencyGuard"/>): the cursor survives
/// redeliveries within one worker process. It is not durable across a worker restart or scaled-out
/// instances — the residual is a rare re-cost of the whole base under those conditions, never a duplicate
/// <i>effect</i> (that is still absorbed by the downstream dedup). A durable backing closes the gap with
/// no change to this interface.</para>
/// </summary>
public interface ICampaignProgressStore
{
    /// <summary>The current cursor + completion state for <paramref name="campaignId"/>.</summary>
    Task<CampaignProgress> GetAsync(string campaignId, CancellationToken ct = default);

    /// <summary>Records that the campaign has fully processed up to and including
    /// <paramref name="lastUserId"/> (the last row of the just-completed page).</summary>
    Task AdvanceAsync(string campaignId, string lastUserId, CancellationToken ct = default);

    /// <summary>Marks the campaign terminal so a later redelivery short-circuits without re-paging.</summary>
    Task MarkCompleteAsync(string campaignId, CancellationToken ct = default);
}

/// <summary>
/// A campaign's resume state. <see cref="LastProcessedUserId"/> is null before the first page completes;
/// <see cref="IsComplete"/> is true once the cursor reached the end of the opted-in set.
/// </summary>
public sealed record CampaignProgress(string? LastProcessedUserId, bool IsComplete);
