using System.Text;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Infra.Common.Configuration.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Clients.Fcm;

/// <summary>
/// FirebaseAdmin-backed implementation of <see cref="IPushDispatcher"/>.
///
/// FirebaseApp is process-singleton (calling <c>FirebaseApp.Create</c> twice
/// throws). We initialize lazily on first dispatch under a lock, then reuse
/// the cached <see cref="FirebaseMessaging"/> instance.
///
/// When <see cref="IFcmConfig.ServiceAccountJson"/> is empty, dispatch is a
/// no-op — keeps dev / CI machines without Firebase wired from crashing the
/// dispatch Function. Production deployments must set the secret.
/// </summary>
public class FcmPushDispatcher(
    IFcmConfig config,
    ILogger<FcmPushDispatcher> logger) : IPushDispatcher
{
    private const string Provider = "Fcm";

    /// <summary>
    /// The one OAuth scope FCM v1 send requires. Pinned explicitly rather than inheriting
    /// FirebaseAdmin's broad DefaultScopes — see the comment at the credential build site.
    /// </summary>
    private const string FcmScope = "https://www.googleapis.com/auth/firebase.messaging";

    /// <summary>
    /// Our own FirebaseApp name. Deliberately NOT the default instance: <c>FirebaseApp.DefaultInstance</c>
    /// may already have been created by something else in the process (a test, another integration) with
    /// a DIFFERENT credential, in which case the credential built here is silently discarded and the
    /// dispatch runs on whatever that other credential can do. A named app makes the credential we built
    /// the credential we use.
    /// </summary>
    private const string AppName = "cleansia-push";

    private static readonly object InitLock = new();
    private static FirebaseMessaging? _messaging;
    private static bool _initAttempted;

    public async Task<PushDispatchResult> SendAsync(
        IReadOnlyList<string> deviceTokens,
        string eventKey,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        if (deviceTokens.Count == 0)
        {
            return new PushDispatchResult(0, 0, []);
        }

        var (messaging, outcome) = EnsureInitialized();
        if (messaging is null)
        {
            return outcome switch
            {
                // No credentials configured — a DELIBERATE no-op in dev / CI. Signal Skipped so the
                // consumer ACKS (no throw, no poison loop): this can never succeed on retry until the
                // secret is provisioned, so retrying to maxDequeueCount would only dead-letter every
                // transactional push.
                InitOutcome.Disabled => SkippedResult(deviceTokens.Count, eventKey),

                // Cold-start FCM-init race (credential refresh / ADC's first OAuth round trip raced the
                // queue trigger). TRANSIENT — surface as all-failed (NOT skipped) so the consumer throws
                // and the queue redelivers; _initAttempted stays false so the next dispatch retries init.
                _ => AllFailedTransientResult(deviceTokens.Count),
            };
        }

        var message = FcmMessageFactory.Build(deviceTokens, eventKey, data);

        BatchResponse response;
        try
        {
            response = await messaging.SendEachForMulticastAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // A credential rejection can surface EITHER per-token (handled below) or as a throw from the
            // whole batch — the OAuth token mint succeeds but FCM answers 401 to the send, and the SDK
            // wraps it. Classify a FirebaseMessagingException with everything it carries so that shape
            // ACKS with a diagnosis too, instead of retrying an unfixable credential to the poison queue.
            var failureClass = ex is FirebaseMessagingException fcmException
                ? IntegrationFailureClassifier.FromFcmException(fcmException)
                : IntegrationFailureClassifier.FromException(ex);
            IntegrationFailureMetrics.Record(Provider, failureClass);
            logger.LogError(ex, "FCM dispatch failed: {FailureClass} for event {EventKey} ({TokenCount} tokens)",
                failureClass, eventKey, deviceTokens.Count);

            if (failureClass == IntegrationFailureClass.AuthConfig)
            {
                var status = ex is FirebaseMessagingException { HttpResponse: { } httpResponse }
                    ? ((int)httpResponse.StatusCode).ToString()
                    : "?";
                var errorCode = (ex as FirebaseMessagingException)?.ErrorCode;
                return new PushDispatchResult(
                    SuccessCount: 0,
                    FailureCount: deviceTokens.Count,
                    InvalidTokens: [],
                    AuthConfig: true,
                    FailureDetail: $"{status} {errorCode}: {ex.Message}");
            }

            // Surface as all-failed (NOT skipped); caller will not prune (no InvalidTokens) and throws so
            // the queue redelivers.
            return AllFailedTransientResult(deviceTokens.Count);
        }

        var invalidTokens = new List<string>();
        var failureCount = 0;
        var authConfigFailures = 0;
        string? authConfigDetail = null;

        for (var i = 0; i < response.Responses.Count; i++)
        {
            var item = response.Responses[i];
            if (item.IsSuccess) continue;

            failureCount++;

            // Surface WHY FCM rejected this token — without this a caller only sees the "{Failure}
            // failed" count and cannot tell an APNs-config problem from a stale token. The
            // MessagingErrorCode is the actionable diagnostic: ThirdPartyAuthError = the APNs auth
            // key/cert in Firebase is wrong/missing/expired or not authorised for the bundle;
            // Unregistered/SenderIdMismatch = stale or wrong-project/wrong-environment token;
            // Unavailable/Internal = transient. It is NULL for a credential rejection (FCM answers
            // 401/403 before its own taxonomy applies), which is why the HTTP status and the
            // transport-level ErrorCode are logged alongside it — those are the only signal in that
            // case, and reading them is the difference between a one-look diagnosis and a poison
            // queue full of synthesized messages. S6-safe: the error codes + the SDK's fixed
            // technical message carry no user content; the token is identified only by its fan-out
            // index.
            var status = (int?)item.Exception?.HttpResponse?.StatusCode;
            logger.LogWarning(
                "FCM rejected token #{Index}/{Total} for event {EventKey}: {ErrorCode}/{TransportErrorCode} " +
                "HTTP {HttpStatus} — {Detail}",
                i + 1, response.Responses.Count, eventKey,
                item.Exception?.MessagingErrorCode, item.Exception?.ErrorCode, status,
                item.Exception?.Message);

            var failureClass = item.Exception is { } tokenException
                ? IntegrationFailureClassifier.FromFcmException(tokenException)
                : IntegrationFailureClass.Transient;
            IntegrationFailureMetrics.Record(Provider, failureClass);

            if (failureClass == IntegrationFailureClass.AuthConfig
                && item.Exception?.MessagingErrorCode is null)
            {
                // Credential-level rejection (as opposed to ThirdPartyAuthError, which is an APNs-key
                // problem inside Firebase and is per-token/per-platform). Remember the provider's own
                // words for the consumer's single alertable log line.
                authConfigFailures++;
                authConfigDetail ??= $"{status?.ToString() ?? "?"} {item.Exception?.ErrorCode}: {item.Exception?.Message}";
            }

            // A dead-token code means the row should be pruned; transient codes leave it in place so
            // FCM can succeed on a retry from the queue. Deliberately keyed on the per-token
            // MessagingErrorCode ONLY: a 401/403 must never prune, or one bad credential would delete
            // every Device row in the database on the next notification.
            if (IntegrationFailureClassifier.IsDeadFcmToken(item.Exception?.MessagingErrorCode))
            {
                invalidTokens.Add(deviceTokens[i]);
            }
        }

        // Every failure was the provider refusing OUR credential, and nothing succeeded → a host-wide
        // config fault. Flag it so the consumer ACKS with one loud, actionable log instead of retrying
        // an unfixable request through maxDequeueCount into the poison queue.
        var allFailedOnAuth = response.SuccessCount == 0
                              && failureCount > 0
                              && authConfigFailures == failureCount;

        return new PushDispatchResult(
            SuccessCount: response.SuccessCount,
            FailureCount: response.FailureCount,
            InvalidTokens: invalidTokens,
            AuthConfig: allFailedOnAuth,
            FailureDetail: allFailedOnAuth ? authConfigDetail : null);
    }

    /// <summary>Why <see cref="EnsureInitialized"/> could not return a live <see cref="FirebaseMessaging"/>.</summary>
    private enum InitOutcome
    {
        /// <summary>A live messaging client is available.</summary>
        Ready,

        /// <summary>No credentials configured (terminal until the secret is provisioned) — the consumer ACKS.</summary>
        Disabled,

        /// <summary>Init threw (cold-start credential/OAuth race) — transient; the consumer THROWS and the queue retries.</summary>
        TransientInitFault,
    }

    /// <summary>Skipped (disabled) result — distinct from all-failed-transient so the consumer can ACK.</summary>
    private static PushDispatchResult SkippedResult(int tokenCount, string eventKey) =>
        new(0, tokenCount, [], Skipped: true);

    /// <summary>All-failed, non-skipped result — the consumer throws and the queue redelivers.</summary>
    private static PushDispatchResult AllFailedTransientResult(int tokenCount) =>
        new(0, tokenCount, []);

    private (FirebaseMessaging? Messaging, InitOutcome Outcome) EnsureInitialized()
    {
        if (_messaging is not null) return (_messaging, InitOutcome.Ready);
        // Latched only on the terminal config-missing path, so a latched null is always Disabled.
        if (_initAttempted) return (null, InitOutcome.Disabled);

        lock (InitLock)
        {
            if (_messaging is not null) return (_messaging, InitOutcome.Ready);
            if (_initAttempted) return (null, InitOutcome.Disabled);

            try
            {
                // Two credential sources, in priority order:
                //  1. FCM:ServiceAccountJson — explicit base64-or-raw JSON of a
                //     downloaded service-account key. Required for any
                //     environment where the SDK can't reach user creds (CI,
                //     prod containers without Workload Identity).
                //  2. Application Default Credentials — picks up
                //     %APPDATA%\gcloud\application_default_credentials.json
                //     (set by `gcloud auth application-default login`) or the
                //     GOOGLE_APPLICATION_CREDENTIALS env var. Used locally
                //     because the GCP org policy iam.disableServiceAccountKey-
                //     Creation blocks downloadable keys on this account.
                //     ADC requires FCM:ProjectId since user creds aren't
                //     project-scoped.
                GoogleCredential credential;
                string? projectIdOverride = null;
                if (!string.IsNullOrWhiteSpace(config.ServiceAccountJson))
                {
                    var raw = config.ServiceAccountJson.TrimStart();
                    var json = raw.StartsWith('{')
                        ? raw
                        : Encoding.UTF8.GetString(Convert.FromBase64String(config.ServiceAccountJson));
                    // Scope the credential EXPLICITLY to FCM. Without CreateScoped, a service-account
                    // credential arrives unscoped, IsCreateScopedRequired stays true, and FirebaseAdmin
                    // substitutes its own six broad DefaultScopes — which historically have NOT included
                    // firebase.messaging, so the minted access token can be rejected by the FCM v1 send
                    // endpoint with a 401 even though the key itself is perfectly valid and the OAuth
                    // mint succeeded. Pinning the scope removes that failure mode and is least-privilege
                    // on its own: we were minting cloud-platform + devstorage.full_control just to send
                    // a push.
                    credential = GoogleCredential.FromJson(json).CreateScoped(FcmScope);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(config.ProjectId))
                    {
                        // Config-missing IS terminal — won't change at runtime,
                        // so latch _initAttempted so we don't spam the warning
                        // on every dispatch.
                        _initAttempted = true;
                        logger.LogWarning(
                            "FCM dispatcher not initialized: neither FCM:ServiceAccountJson nor " +
                            "FCM:ProjectId configured. Run `gcloud auth application-default login` " +
                            "and set FCM:ProjectId user-secret to enable ADC-based dispatch.");
                        return (null, InitOutcome.Disabled);
                    }
                    credential = GoogleCredential.GetApplicationDefault();
                    projectIdOverride = config.ProjectId;
                }

                var options = new AppOptions { Credential = credential };
                if (projectIdOverride is not null) options.ProjectId = projectIdOverride;

                var app = FirebaseApp.GetInstance(AppName) ?? FirebaseApp.Create(options, AppName);
                _messaging = FirebaseMessaging.GetMessaging(app);
                return (_messaging, InitOutcome.Ready);
            }
            catch (Exception ex)
            {
                // Transient — credential refresh can fail on cold start before
                // the network is fully reachable, or ADC's first OAuth round
                // trip races with the queue trigger. Leave _initAttempted
                // false so the NEXT dispatch retries; FCM dispatch is at-most-
                // once today anyway, and the queue will redeliver. NOT skipped:
                // the consumer must THROW so the message redelivers.
                logger.LogError(ex, "Failed to initialize Firebase Admin SDK (will retry on next dispatch)");
                return (null, InitOutcome.TransientInitFault);
            }
        }
    }
}
