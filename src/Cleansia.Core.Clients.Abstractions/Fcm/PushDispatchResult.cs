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
public record PushDispatchResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> InvalidTokens,
    bool Skipped = false);
