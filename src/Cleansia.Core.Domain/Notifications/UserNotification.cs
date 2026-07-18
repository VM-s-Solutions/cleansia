using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// One in-app feed row per targeted push send, written by the PRODUCER inside the same unit of
/// work as the domain change and the outbox push row — the feed records business truth, so the
/// row exists iff the event the user reads about committed. Push delivery is deliberately
/// at-most-once and keeps nothing, so this row is the only durable record of the notification.
///
/// <see cref="ArgsJson"/> is exactly the push <c>Args</c> dictionary (loc-args, never PII, never
/// rendered text — clients render title/body from their bundled templates in the device locale).
/// <see cref="Auditable.CreatedOn"/> null-<see cref="ReadOn"/> = unread.
/// </summary>
public class UserNotification : Auditable, ITenantEntity
{
    public string UserId { get; private set; } = default!;

    public string EventKey { get; private set; } = default!;

    public string ArgsJson { get; private set; } = "{}";

    public DateTimeOffset? ReadOn { get; private set; }

    private UserNotification()
    {
    }

    public static UserNotification Create(string userId, string eventKey, string argsJson, string? tenantId)
    {
        return new UserNotification
        {
            UserId = userId,
            EventKey = eventKey,
            ArgsJson = argsJson,
            TenantId = tenantId,
        };
    }

    public void MarkRead(DateTimeOffset readOn)
    {
        // Idempotent — the first read timestamp wins.
        ReadOn ??= readOn;
    }

    /// <summary>
    /// Digest collapse: a repeat send while this row is unread updates it in place (fresh args,
    /// fresh timestamp) instead of stacking a second unread row — max one unread digest per user.
    /// </summary>
    public void RefreshDigest(string argsJson, DateTimeOffset refreshedOn)
    {
        ArgsJson = argsJson;
        Created(CreatedBy, refreshedOn);
    }
}
