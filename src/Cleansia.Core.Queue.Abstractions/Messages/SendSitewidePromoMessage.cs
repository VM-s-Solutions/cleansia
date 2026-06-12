using System.Linq;

namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// One queue message per "send sitewide promo" admin action. The consumer
/// Function pages through users with <c>Promo = true</c> and enqueues one
/// <see cref="SendPushNotificationMessage"/> per recipient on
/// <c>notifications-dispatch</c>, carrying the locale-matched title+body
/// in <c>Args</c>.
///
/// Unlike other Phase A/B events whose body is a fixed template resolved
/// on the mobile side via <c>strings.xml</c>, this event's body is
/// admin-authored at send time. Mobile receives the already-localized text
/// in the FCM data payload (<c>title</c> + <c>body</c> args) and bypasses
/// the local template lookup.
///
/// Fan-out (one user → one notification queue message) lives in the
/// Function consumer rather than the synchronous request handler because:
///   - The admin request returns immediately (no blocked HTTP roundtrip
///     during a million-user dispatch).
///   - Azure Storage Queues batch ~10 messages/s/partition on the default
///     SKU; the consumer can throttle the fan-out without back-pressuring
///     the admin caller.
///   - Failures during fan-out retry via the queue's poison-message
///     pipeline instead of failing the admin form submit.
/// </summary>
public record SendSitewidePromoMessage(
    /// <summary>Locale-keyed titles (en/cs/sk/uk/ru). Each value already
    /// authored by the admin in the matching language. Missing keys
    /// fall back to <c>en</c>.</summary>
    Dictionary<string, string> TitleByLocale,
    /// <summary>Locale-keyed bodies, same shape as
    /// <see cref="TitleByLocale"/>.</summary>
    Dictionary<string, string> BodyByLocale,
    /// <summary>Tenant the campaign targets. Cross-tenant sends are
    /// intentionally not supported — one campaign = one tenant.</summary>
    string? TenantId,
    /// <summary>Deterministic per-campaign identity. A pure function of the
    /// campaign's domain inputs (tenant + the per-locale title/body the admin
    /// authored) via <see cref="DeriveCampaignId"/> — never a fresh Guid. The
    /// producer dedup (a double-submitted admin action collapses onto one
    /// fan-out) and the consumer resume cursor (a redelivery resumes, not
    /// restarts) both key on it.</summary>
    string CampaignId)
{
    private const char FieldSeparator = '';

    /// <summary>
    /// The campaign's stable identity. An admin-authored send has no domain id, so its only stable
    /// identity is its content: the tenant plus the per-locale title+body. Same inputs ⇒ same id (no
    /// Guid/timestamp). Shares the frozen <c>promo:{tenant}:{contentHash}</c> shape with
    /// <c>MessageKeys.SitewidePromo</c> so the campaign id and the outbox message key are one value.
    /// </summary>
    public static string DeriveCampaignId(
        string? tenantId,
        IReadOnlyDictionary<string, string> titleByLocale,
        IReadOnlyDictionary<string, string> bodyByLocale) =>
        MessageKeys.SitewidePromo(tenantId, ContentSignature(titleByLocale, bodyByLocale));

    private static string ContentSignature(
        IReadOnlyDictionary<string, string> titleByLocale,
        IReadOnlyDictionary<string, string> bodyByLocale) =>
        string.Join(FieldSeparator, titleByLocale.Values.Concat(bodyByLocale.Values));
}
