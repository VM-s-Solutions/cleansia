using System.Net;
using System.Text;
using System.Text.Json;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Clients.Abstractions.Apns;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Clients.Apns;

/// <summary>
/// Direct-APNs implementation of <see cref="ILiveActivityPushClient"/> (ADR-0029 D1). HTTP/2 POST to
/// <c>/3/device/{token}</c> on <c>api{.sandbox}.push.apple.com</c>, authenticated with the ES256
/// provider JWT (<see cref="IApnsJwtProvider"/>), carrying the <c>liveactivity</c> push-type + the
/// derived <c>{bundle}.push-type.liveactivity</c> topic. The transport is a pooled, named
/// <see cref="IHttpClientFactory"/> client (ADR-0005 D1).
///
/// <para>INERT when off: <c>APNS:Enabled=false</c>, empty key material, or key material that cannot be
/// parsed into a signing key (an unresolved <c>@Microsoft.KeyVault(…)</c> reference) → the client returns
/// Skipped WITHOUT opening a socket. The <c>.p8</c>/JWT never appear in a log line or an exception message
/// (S6): failures are logged by APNs status + APNs <c>reason</c> only.</para>
/// </summary>
public sealed class ApnsLiveActivityClient(
    IApnsConfig config,
    IApnsJwtProvider jwtProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<ApnsLiveActivityClient> logger) : ILiveActivityPushClient
{
    /// <summary>
    /// The named <see cref="IHttpClientFactory"/> client whose pooled <c>SocketsHttpHandler</c> the
    /// HTTP/2 APNs transport is built on (ADR-0005 D1).
    /// </summary>
    public const string HttpClientName = "Apns";

    private const string Provider = "Apns";
    private const string SandboxHost = "https://api.sandbox.push.apple.com";
    private const string ProductionHost = "https://api.push.apple.com";

    private static readonly JsonSerializerOptions BodyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<LiveActivityPushResult> SendAsync(string activityToken, LiveActivityPush push, CancellationToken cancellationToken)
    {
        // INERT ship: disabled or keyless never opens a socket (ADR-0029 D1). Same Skipped-ack no-op the
        // FCM path implements for an unconfigured provider.
        if (!config.Enabled
            || string.IsNullOrWhiteSpace(config.PrivateKeyPem)
            || string.IsNullOrWhiteSpace(config.KeyId)
            || string.IsNullOrWhiteSpace(config.TeamId)
            || string.IsNullOrWhiteSpace(config.CustomerBundleId))
        {
            if (config.Enabled)
            {
                // Enabled but keyless — a config gap ops must close. Warn (once per send is acceptable;
                // the channel is gated on token existence upstream so this is rare) and Skip, never crash.
                logger.LogWarning(
                    "APNS enabled but key material incomplete (KeyId/TeamId/PrivateKeyPem/CustomerBundleId) — skipping live-activity send");
            }
            return LiveActivityPushResult.SkippedResult();
        }

        // Key material is present but UNPARSEABLE — e.g. an unresolved "@Microsoft.KeyVault(SecretUri=…)"
        // reference App Service hands through verbatim before the secret is seeded, which is non-empty (so it
        // clears the guard above) yet neither PEM nor base64. Degrade to the SAME Skipped path as keyless
        // rather than let a FormatException out of GetToken() flow to the consumer and poison-storm the queue
        // (ADR-0029 D1: unusable-while-Enabled → Skipped + one Warning, never a crash). S6: never log the value.
        if (!jwtProvider.HasUsableKey())
        {
            logger.LogWarning(
                "APNS enabled but the private key is not a usable .p8 (looks like an unresolved secret reference?) — skipping live-activity send");
            return LiveActivityPushResult.SkippedResult();
        }

        var body = JsonSerializer.Serialize(BuildApsBody(push), BodyOptions);

        // One re-mint retry on an expired/invalid provider token (403), then classify transient.
        for (var attempt = 0; ; attempt++)
        {
            var response = await SendOnceAsync(activityToken, push, body, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return LiveActivityPushResult.Sent();
            }

            var status = (int)response.StatusCode;
            var reason = await ReadReasonAsync(response, cancellationToken);

            // 403 ExpiredProviderToken/InvalidProviderToken — re-mint ONCE, then fall through to transient.
            if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0)
            {
                logger.LogWarning("APNS rejected the provider token ({Reason}) — re-minting once and retrying", reason);
                jwtProvider.Invalidate();
                continue;
            }

            var failureClass = IntegrationFailureClassifier.FromHttpStatus(status);
            IntegrationFailureMetrics.Record(Provider, failureClass);

            // Permanent, TOKEN-specific → prune the row (410 Unregistered / 400 BadDeviceToken).
            if (status == 410 || (status == 400 && string.Equals(reason, "BadDeviceToken", StringComparison.Ordinal)))
            {
                logger.LogInformation("APNS rejected activity token as permanently invalid ({Status} {Reason}) — pruning", status, reason);
                return LiveActivityPushResult.InvalidToken();
            }

            // Transient → THROW so the queue redelivers (429/5xx/network + a 403 that survived the re-mint).
            if (failureClass.IsRetryable() || response.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogError("APNS live-activity send failed transiently ({Status} {Reason}) — retrying via queue", status, reason);
                throw new HttpRequestException($"APNS transient failure: {status} {reason}");
            }

            // Permanent, NON-token (e.g. BadTopic) → ack; retrying can never succeed. An ops config issue.
            logger.LogError("APNS live-activity send rejected permanently ({Status} {Reason}) — acking (config issue)", status, reason);
            return LiveActivityPushResult.PermanentFailure();
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        string activityToken, LiveActivityPush push, string body, CancellationToken cancellationToken)
    {
        var host = config.UseSandbox ? SandboxHost : ProductionHost;
        var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/3/device/{activityToken}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("authorization", $"bearer {jwtProvider.GetToken()}");
        request.Headers.TryAddWithoutValidation("apns-push-type", "liveactivity");
        request.Headers.TryAddWithoutValidation("apns-topic", $"{config.CustomerBundleId}.push-type.liveactivity");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");
        // Bound APNs storage/retry to the window the update is still meaningful (the stale-date).
        request.Headers.TryAddWithoutValidation("apns-expiration", push.StaleDate.ToUnixTimeSeconds().ToString());

        var client = httpClientFactory.CreateClient(HttpClientName);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<string?> ReadReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload)) return null;
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
        }
        catch (Exception)
        {
            // A non-JSON error body carries no actionable reason; the status code alone classifies it.
            return null;
        }
    }

    /// <summary>
    /// The <c>aps</c> envelope. Dictionary keys are the literal ActivityKit wire keys (hyphenated —
    /// they are NOT re-cased by the camelCase policy, which applies to POCO properties only, so the
    /// content-state/attributes keys become v/status/orderNumber/… while content-state/stale-date/
    /// dismissal-date/attributes-type stay verbatim).
    /// </summary>
    private static object BuildApsBody(LiveActivityPush push)
    {
        var aps = new Dictionary<string, object?>
        {
            ["timestamp"] = push.Timestamp.ToUnixTimeSeconds(),
            ["event"] = push.Event,
            ["content-state"] = push.ContentState,
            ["stale-date"] = push.StaleDate.ToUnixTimeSeconds(),
        };

        if (push.DismissalDate is { } dismissal)
        {
            aps["dismissal-date"] = dismissal.ToUnixTimeSeconds();
        }

        if (push.AttributesType is not null && push.Attributes is not null)
        {
            aps["attributes-type"] = push.AttributesType;
            aps["attributes"] = push.Attributes;
        }

        return new Dictionary<string, object?> { ["aps"] = aps };
    }
}
