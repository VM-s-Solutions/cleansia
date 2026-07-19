namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single seam every push-producing site calls. One call atomically records BOTH halves of a
/// user notification into the caller's unit of work: the in-app feed row (for feed-scoped events)
/// and the outbox push message — so neither exists unless the producing transaction commits, and
/// no producer can send a push without its feed row. Constructing a
/// <c>SendPushNotificationMessage</c> anywhere else (outside the sitewide-promo fan-out) is a
/// violation, pinned by a raw-file tripwire test.
///
/// Category mutes gate the PUSH (checked by the dispatch consumer), never the feed row — the one
/// exception is the new-jobs digest, whose producer already skips muted cleaners entirely.
/// </summary>
public interface INotificationProducer
{
    /// <summary>
    /// Records the notification. <paramref name="args"/> is the loc-args dictionary clients render
    /// templates from (never PII); <paramref name="subject"/> is the push dedup key's subject
    /// segment (typically the order/dispute/membership id the event is about).
    /// </summary>
    Task NotifyAsync(
        string userId,
        string eventKey,
        Dictionary<string, string> args,
        string? tenantId,
        string? subject,
        CancellationToken cancellationToken);
}
