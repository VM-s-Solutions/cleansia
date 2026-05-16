using System.Text;
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
            logger.LogError(ex, "FCM dispatch failed for event {EventKey} ({TokenCount} tokens)",
                eventKey, deviceTokens.Count);
            // Surface as all-failed; caller will not prune (no InvalidTokens).
            return new PushDispatchResult(0, deviceTokens.Count, []);
        }

        var invalidTokens = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var item = response.Responses[i];
            if (item.IsSuccess) continue;

            // FCM permanent-failure error codes that mean "this token is dead":
            //   - Unregistered: app uninstalled or token rotated
            //   - InvalidArgument: malformed token
            //   - SenderIdMismatch: token belongs to a different Firebase project
            // All other codes (Unavailable, Internal, etc.) are transient — leave
            // the row in place; FCM will succeed on a retry from the queue.
            var code = item.Exception?.MessagingErrorCode;
            if (code is MessagingErrorCode.Unregistered
                     or MessagingErrorCode.InvalidArgument
                     or MessagingErrorCode.SenderIdMismatch)
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
            _initAttempted = true;

            if (string.IsNullOrWhiteSpace(config.ServiceAccountJson))
            {
                return null;
            }

            try
            {
                // Accept base64-encoded JSON (preferred) or raw JSON (legacy).
                var raw = config.ServiceAccountJson.TrimStart();
                var json = raw.StartsWith('{')
                    ? raw
                    : Encoding.UTF8.GetString(Convert.FromBase64String(config.ServiceAccountJson));

                var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(json),
                });
                _messaging = FirebaseMessaging.GetMessaging(app);
                return _messaging;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Firebase Admin SDK");
                return null;
            }
        }
    }
}
