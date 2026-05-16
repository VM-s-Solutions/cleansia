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
    string? TenantId);
