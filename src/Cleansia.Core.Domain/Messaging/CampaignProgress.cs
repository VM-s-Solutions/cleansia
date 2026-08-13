using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Messaging;

/// <summary>
/// Durable per-campaign resume marker. The fan-out consumer pages in a stable order and advances the
/// cursor per page, so a redelivery resumes rather than restarting, and a redelivery of a finished
/// campaign is a no-op.
///
/// <para><b>Tenant-global by design — deliberately NOT an <c>ITenantEntity</c></b>, a reasoned exception:
/// the consumer is a system process with no tenant in context and the campaign id is globally unique.
/// → /flows/cross-cutting#tenancy</para>
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
