namespace Cleansia.Core.Clients.Abstractions.Fcm;

/// <summary>
/// Sends a data-only FCM push to one or more device tokens.
///
/// Data-only payloads are intentional — the mobile client localizes the
/// title/body via <c>strings.xml</c>, so the lock-screen text is whatever
/// WE choose to show, not whatever the server happens to inject. This also
/// keeps PII (customer name, address) off the wire.
/// </summary>
public interface IPushDispatcher
{
    /// <summary>
    /// Send a single payload to a batch of device tokens. Returns a result
    /// the caller uses to prune dead tokens.
    ///
    /// <para>The result's <see cref="PushDispatchResult.Skipped"/> flag DISTINGUISHES a deliberate no-op
    /// (the provider is unconfigured — dev / CI) from an all-failed-transient outcome (a cold-start init
    /// race or a network fault). A skipped dispatch can never succeed on retry until the secret is
    /// provisioned, so the consumer ACKS it; an all-failed (non-skipped) result is retryable, so the
    /// consumer throws and the queue redelivers.</para>
    /// </summary>
    /// <param name="deviceTokens">FCM registration tokens. Caller is
    /// responsible for filtering by user preference + NotificationsEnabled
    /// before invoking; this method blindly attempts every supplied token.</param>
    /// <param name="eventKey">Event key (e.g. <c>order.confirmed</c>) the
    /// mobile client uses to look up localized strings. Lands in the data
    /// payload as <c>event_key</c>.</param>
    /// <param name="data">Structured args (e.g. <c>orderId</c>,
    /// <c>orderNumber</c>) the mobile client substitutes into the
    /// localized template. Caller must NOT include PII.</param>
    Task<PushDispatchResult> SendAsync(
        IReadOnlyList<string> deviceTokens,
        string eventKey,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
}
