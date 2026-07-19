namespace Cleansia.Core.Clients.Abstractions.Apns;

/// <summary>
/// Signs and delivers ONE ActivityKit payload to ONE activity push token over direct APNs (ADR-0029
/// D1) — the token-authenticated (<c>.p8</c>/ES256) <c>liveactivity</c> channel beside FCM. Owns the
/// provider-JWT cache + the failure taxonomy; DOES NOT know orders, tenants, or which transition it is
/// carrying (that is the dispatch consumer's job).
///
/// <para>Taxonomy (ADR-0029 D1): <c>Enabled=false</c> or empty key → <see cref="LiveActivityPushResult.SkippedResult"/>
/// (never opens a socket); <c>410 Unregistered</c>/<c>400 BadDeviceToken</c> →
/// <see cref="LiveActivityPushResult.InvalidToken"/> (prune the row); <c>403</c> Expired/Invalid provider
/// token → re-mint the JWT once, then classify transient; <c>429</c>/<c>5xx</c>/network → THROW (the
/// queue redelivers). Other permanent 4xx → <see cref="LiveActivityPushResult.PermanentFailure"/>.</para>
/// </summary>
public interface ILiveActivityPushClient
{
    /// <summary>
    /// Deliver <paramref name="push"/> to <paramref name="activityToken"/>. Returns the terminal,
    /// non-throwing outcome; THROWS on a transient failure so the caller lets the queue redeliver.
    /// </summary>
    Task<LiveActivityPushResult> SendAsync(string activityToken, LiveActivityPush push, CancellationToken cancellationToken);
}
