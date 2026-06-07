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

        var messaging = EnsureInitialized();
        if (messaging is null)
        {
            // No service-account configured — silently skip in dev. Production
            // callers should ensure the config secret is provisioned before
            // shipping; we don't fail noisy here because that would block every
            // upstream order/dispute commit on a config gap.
            logger.LogWarning(
                "FCM dispatcher not initialized (FCM:ServiceAccountJson empty). " +
                "Skipping {TokenCount} tokens for event {EventKey}.",
                deviceTokens.Count, eventKey);
            return new PushDispatchResult(0, deviceTokens.Count, []);
        }

        var payload = new Dictionary<string, string>(data) { ["event_key"] = eventKey };

        var message = new MulticastMessage
        {
            Tokens = deviceTokens.ToArray(),
            Data = payload,
            // Android-specific: high-priority so transactional events
            // wake the device immediately. Marketing pushes (Promo) should
            // override this — caller controls per-event.
            Android = new AndroidConfig
            {
                Priority = Priority.High,
            },
        };

        BatchResponse response;
        try
        {
            response = await messaging.SendEachForMulticastAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            var failureClass = IntegrationFailureClassifier.FromException(ex);
            IntegrationFailureMetrics.Record(Provider, failureClass);
            logger.LogError(ex, "FCM dispatch failed: {FailureClass} for event {EventKey} ({TokenCount} tokens)",
                failureClass, eventKey, deviceTokens.Count);
            // Surface as all-failed; caller will not prune (no InvalidTokens) and the queue redelivers.
            return new PushDispatchResult(0, deviceTokens.Count, []);
        }

        var invalidTokens = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var item = response.Responses[i];
            if (item.IsSuccess) continue;

            // A dead-token code means the row should be pruned; transient codes leave it in place so
            // FCM can succeed on a retry from the queue.
            if (IntegrationFailureClassifier.IsDeadFcmToken(item.Exception?.MessagingErrorCode))
            {
                invalidTokens.Add(deviceTokens[i]);
            }
        }

        return new PushDispatchResult(
            SuccessCount: response.SuccessCount,
            FailureCount: response.FailureCount,
            InvalidTokens: invalidTokens);
    }

    private FirebaseMessaging? EnsureInitialized()
    {
        if (_messaging is not null) return _messaging;
        if (_initAttempted) return null;

        lock (InitLock)
        {
            if (_messaging is not null) return _messaging;
            if (_initAttempted) return null;

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
                    credential = GoogleCredential.FromJson(json);
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
                        return null;
                    }
                    credential = GoogleCredential.GetApplicationDefault();
                    projectIdOverride = config.ProjectId;
                }

                var options = new AppOptions { Credential = credential };
                if (projectIdOverride is not null) options.ProjectId = projectIdOverride;

                var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(options);
                _messaging = FirebaseMessaging.GetMessaging(app);
                return _messaging;
            }
            catch (Exception ex)
            {
                // Transient — credential refresh can fail on cold start before
                // the network is fully reachable, or ADC's first OAuth round
                // trip races with the queue trigger. Leave _initAttempted
                // false so the NEXT dispatch retries; FCM dispatch is at-most-
                // once today anyway, and the queue will redeliver.
                logger.LogError(ex, "Failed to initialize Firebase Admin SDK (will retry on next dispatch)");
                return null;
            }
        }
    }
}
