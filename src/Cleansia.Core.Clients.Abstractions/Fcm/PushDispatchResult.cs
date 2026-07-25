namespace Cleansia.Core.Clients.Abstractions.Fcm;

/// <summary>
/// Outcome of a multi-token FCM send. The dispatch Function uses
/// <see cref="InvalidTokens"/> to prune dead <c>Device</c> rows after
/// FCM returns 410 / NotRegistered for a token.
/// </summary>
/// <param name="SuccessCount">Tokens that received the push.</param>
/// <param name="FailureCount">Tokens that failed for any reason.</param>
/// <param name="InvalidTokens">
/// Subset of input tokens that FCM rejected as permanently invalid
/// (NotRegistered, InvalidArgument, etc.). Caller deletes the matching
/// <c>Device</c> rows.
/// </param>
/// <param name="Skipped">
/// True when dispatch was a DELIBERATE NO-OP because the provider is unconfigured
/// (e.g. FCM:ServiceAccountJson / FCM:ProjectId not set in dev / CI). This is
/// DISTINCT from "all tokens failed transiently": a skipped dispatch will never
/// succeed on retry until the secret is provisioned, so the consumer must ACK it
/// (no throw, no poison loop) rather than treat it as a retryable all-failed result.
/// A genuine cold-start FCM-init race is NOT skipped — it returns an all-failed
/// (non-skipped) result so the consumer still throws and the queue redelivers.
/// </param>
/// <param name="AuthConfig">
/// True when EVERY token failed because the provider REJECTED OUR CREDENTIAL (FCM answered
/// 401/403 — a wrong/disabled service-account key, a missing OAuth scope, or the FCM API not
/// enabled on the project). Like <see cref="Skipped"/> this is a permanent config fault the
/// consumer must ACK rather than throw on, but for the opposite reason: the provider IS
/// configured, it just says no. Retrying cannot succeed — a 401 is host-wide, so every push in
/// the system is failing identically and redelivery is amplification, not recovery. It is also
/// DISTINCT from <see cref="InvalidTokens"/>: the tokens are innocent, so nothing is pruned.
/// </param>
/// <param name="FailureDetail">
/// The provider's own error text for an <see cref="AuthConfig"/> (or otherwise notable) failure,
/// e.g. <c>"401 Unauthenticated: Request had invalid authentication credentials."</c>. Carried so
/// the consumer can log the ACTIONABLE cause instead of a synthesized message. S6-safe: FCM's
/// technical error text carries no user content, and device tokens are never put in it.
/// </param>
public record PushDispatchResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> InvalidTokens,
    bool Skipped = false,
    bool AuthConfig = false,
    string? FailureDetail = null);
