namespace Cleansia.Core.Clients.Abstractions.Apns;

/// <summary>
/// Outcome of ONE ActivityKit send to ONE activity token (ADR-0029 D1 taxonomy). A SINGLE-token
/// sibling of <c>PushDispatchResult</c>. TRANSIENT failures (429/5xx/network) are NOT a result — the
/// client THROWS so the queue redelivers; only the terminal, non-throwing outcomes surface here.
/// </summary>
/// <param name="Delivered">APNs accepted the payload (2xx).</param>
/// <param name="Skipped">
/// DELIBERATE no-op — the provider is disabled (<c>APNS:Enabled=false</c>) or its key material is
/// empty. The client never opened a socket; the consumer acks (rows untouched). This is how the channel
/// ships INERT. A provider-level state (applies to every token), distinct from a transient all-fail.
/// </param>
/// <param name="TokenInvalid">
/// APNs rejected the token as PERMANENTLY invalid (<c>410 Unregistered</c> / <c>400 BadDeviceToken</c>).
/// The consumer prunes the matching <c>LiveActivityToken</c> row and acks — retrying can never succeed.
/// </param>
public sealed record LiveActivityPushResult(
    bool Delivered,
    bool Skipped = false,
    bool TokenInvalid = false)
{
    /// <summary>APNs accepted the payload (2xx).</summary>
    public static LiveActivityPushResult Sent() => new(Delivered: true);

    /// <summary>Provider disabled / keyless — never opened a socket; the consumer acks (no prune).</summary>
    public static LiveActivityPushResult SkippedResult() => new(Delivered: false, Skipped: true);

    /// <summary>Permanently-invalid token (410/BadDeviceToken) — the consumer prunes the row and acks.</summary>
    public static LiveActivityPushResult InvalidToken() => new(Delivered: false, TokenInvalid: true);

    /// <summary>
    /// A permanent, non-token rejection (e.g. <c>BadTopic</c>) — retrying can never succeed, so the
    /// consumer acks. Already logged at Error by the client (an ops config issue, not a token issue).
    /// </summary>
    public static LiveActivityPushResult PermanentFailure() => new(Delivered: false);
}
