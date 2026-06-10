using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Messaging;

/// <summary>
/// Durable per-campaign resume marker (the durable backing for
/// <see cref="Queue.Abstractions.ICampaignProgressStore"/>). One row per sitewide-promo campaign: the
/// fan-out consumer pages opted-in users in a stable <c>OrderBy(UserId)</c> order and advances the
/// cursor after each page, so a redelivery resumes past <see cref="LastProcessedUserId"/> instead of
/// restarting at offset 0 (a re-cost), and a redelivery of a completed campaign is a no-op.
///
/// <para><b>Tenant-global by design — intentionally NOT <see cref="ITenantEntity"/> (a reasoned S8
/// exception, same as <see cref="ProcessedMessage"/>).</b> The fan-out consumer is a system process with
/// no JWT/tenant in context, and <see cref="CampaignId"/> is globally unique. A composite
/// <c>(TenantId, CampaignId)</c> key would only weaken the lookup for the null-tenant rows the consumer
/// writes.</para>
///
/// <para>This is a Bucket-C cost/spam layer, NOT an at-most-once effect control — the downstream
/// <c>push:{UserId}:{EventKey}</c> guard (the <see cref="ProcessedMessage"/> claim) is the effect
/// control. A rare first-advance race surfacing as a 23505 is a benign re-cost, so the store does a
/// find-or-insert-then-update rather than relying on the unique index to gate an effect.</para>
/// </summary>
public class CampaignProgress : BaseEntity
{
    /// <summary>
    /// The globally-unique campaign id (the resume-marker key). The unique index makes the one-row-per-
    /// campaign invariant load-bearing. Capped at 128 chars.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string CampaignId { get; private set; } = default!;

    /// <summary>
    /// The last fully-processed recipient <c>UserId</c> (the last row of the last completed page).
    /// <c>null</c> before the first page completes. Capped at 26 (ULID length).
    /// </summary>
    [MaxLength(26)]
    public string? LastProcessedUserId { get; private set; }

    /// <summary>True once the cursor reached the end of the opted-in set (the campaign is terminal).</summary>
    public bool IsComplete { get; private set; }

    public static CampaignProgress Create(string campaignId)
        => new()
        {
            CampaignId = campaignId,
            LastProcessedUserId = null,
            IsComplete = false,
        };

    /// <summary>Records that the campaign has fully processed up to and including <paramref name="lastUserId"/>.</summary>
    public void Advance(string lastUserId) => LastProcessedUserId = lastUserId;

    /// <summary>Marks the campaign terminal so a later redelivery short-circuits without re-paging.</summary>
    public void MarkComplete() => IsComplete = true;
}
