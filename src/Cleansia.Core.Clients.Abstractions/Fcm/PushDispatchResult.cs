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
/// True when EVERY token failed on a CREDENTIAL rejection. TWO different credentials sit on this
/// path and both surface as an FCM 401 — only the FCM error code tells them apart:
/// <list type="bullet">
/// <item><description>GOOGLE refused us — a wrong/disabled service-account key, a missing OAuth
/// scope, or the FCM API not enabled on the project. Arrives with NO FCM error code, because the
/// failure is at the auth layer before FCM's own taxonomy applies.</description></item>
/// <item><description>APPLE refused Firebase — <c>ThirdPartyAuthError</c>. FCM authenticated to
/// Google perfectly well; APNs then rejected the APNs auth key stored in FIREBASE. Usually the key
/// is scoped to the Sandbox environment while the build is Production (TestFlight / App Store), or
/// it is revoked, or its Key ID / Team ID do not match. Nothing in Azure, Key Vault or GCP is
/// involved and no redeploy can affect it.</description></item>
/// </list>
/// Like <see cref="Skipped"/> this is a permanent config fault the consumer must ACK rather than
/// throw on, but for the opposite reason: the provider IS configured, it just says no. Retrying
/// cannot succeed — the fault is host-wide, so every push in the system is failing identically and
/// redelivery is amplification, not recovery. It is also DISTINCT from <see cref="InvalidTokens"/>:
/// the tokens are innocent, so nothing is pruned.
/// </param>
/// <param name="FailureDetail">
/// The actionable diagnosis for an <see cref="AuthConfig"/> (or otherwise notable) failure: the
/// provider's own error text, and on the Apple branch a statement of WHICH credential was refused
/// and what to check. Carried so the consumer can log the real cause rather than a synthesized
/// message that names the wrong system. S6-safe: FCM's technical error text carries no user content,
/// and device tokens are never put in it.
/// </param>
public record PushDispatchResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> InvalidTokens,
    bool Skipped = false,
    bool AuthConfig = false,
    string? FailureDetail = null);
