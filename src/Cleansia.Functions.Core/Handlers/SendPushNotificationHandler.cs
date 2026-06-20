using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes <see cref="SendPushNotificationMessage"/> from
/// <c>notifications-dispatch</c>, resolves the user's active devices that
/// allow this category, fans out via <see cref="IPushDispatcher"/>, and
/// prunes any <c>Device</c> rows whose tokens FCM rejected as permanently
/// invalid.
///
/// <para>ADR-0002 D2.2 — GUARD-FIRST (claim-then-act). FCM is non-transactional and a push has no
/// domain target-state to re-check, so before the terminal send the consumer claims the deterministic
/// D2.1 key via <see cref="IIdempotencyGuard"/>; a redelivery or a duplicate enqueue collapses onto the
/// same key and short-circuits. The guarantee is AT-MOST-ONCE AFTER THE MARKER — a crash between the
/// claim and the FCM send loses that one push, which is accepted for a notification (a user can be
/// re-notified by the next event) and is NEVER acceptable for a fiscal artifact. Do not mistake this
/// guard for exactly-once.</para>
///
/// Mirrors <c>SendEmailHandler</c> / <c>GenerateReceiptHandler</c>: queue-trigger, no JWT context,
/// cross-tenant lookup with override on the <c>ITenantProvider</c>.
/// </summary>
public class SendPushNotificationHandler(
    IDeviceRepository deviceRepository,
    IUserNotificationPreferencesRepository preferencesRepository,
    IPushDispatcher pushDispatcher,
    IUnitOfWork unitOfWork,
    IIdempotencyGuard idempotencyGuard,
    ITenantProvider tenantProvider,
    ILogger<SendPushNotificationHandler> logger)
{
    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        // ── ADR-0002 D3.3 — failure CLASSIFICATION phase 1: PERMANENT failures ACK ──
        // A malformed / un-deserializable body or a business-rejected message (missing required fields)
        // can never succeed on retry, so it is logged at Warning and ACKED (return) — it must not reach
        // the transient catch and burn every dequeue into the poison queue.
        // ADR-0002 D2.1a — DUAL-READ at the deploy boundary. Producers wrap the payload in a
        // QueueEnvelope<T> ({"messageKey","tenantId","payload":{...}}) whose MessageKey is the
        // authoritative D2.1 claim key; bare pre-envelope messages may still be in-flight, so fall back
        // to the bare body and SYNTHESIZE the same key from the payload.
        SendPushNotificationMessage? message;
        string? envelopeTenantId;
        string? envelopeMessageKey;
        try
        {
            (message, envelopeTenantId, envelopeMessageKey) = ReadPayload(messageText);
        }
        catch (JsonException ex)
        {
            // S6: never log the raw body — it carries PII / push content. Log only its size.
            logger.LogWarning(ex,
                "Discarding push message: malformed/un-deserializable body (permanent), {Bytes} bytes",
                messageText.Length);
            return;
        }

        if (message is null)
        {
            logger.LogWarning(
                "Discarding push message: deserialized to null (permanent), {Bytes} bytes",
                messageText.Length);
            return;
        }

        if (string.IsNullOrEmpty(message.UserId) || string.IsNullOrEmpty(message.EventKey))
        {
            logger.LogWarning(
                "Discarding push message with missing UserId or EventKey ({Bytes} bytes)",
                messageText.Length);
            return;
        }

        // ── ADR-0002 D2.2 — CLAIM-FIRST, before any tenant-scoped read or the FCM send ──
        // Prefer the envelope's authoritative MessageKey; for a bare in-flight body synthesize the same
        // deterministic D2.1 key from the payload. The claim is unconditional (it is NOT gated behind the
        // dead-token prune below). If the key is already claimed the effect already ran → ack and return,
        // no push sent.
        var messageKey = !string.IsNullOrEmpty(envelopeMessageKey)
            ? envelopeMessageKey
            : MessageKeys.Push(message.UserId, message.EventKey, SubjectFrom(message));

        if (await idempotencyGuard.AlreadyProcessedAsync(messageKey, ct))
        {
            logger.LogInformation("Push {MessageKey} already dispatched, skipping (idempotent)", messageKey);
            return;
        }

        try
        {
            // Cross-tenant lookup — the queue trigger has no JWT, so the EF global filter has no tenant.
            // The envelope's TenantId (when present) is authoritative; fall back to the payload.
            var tenantId = !string.IsNullOrEmpty(envelopeTenantId) ? envelopeTenantId : message.TenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                tenantProvider.SetTenantOverride(tenantId);
            }

            // Per-user category gating. No prefs row = treat as defaults
            // (everything but Promo is on). The dispatch ALSO checks the
            // category-bool in case the row exists but the category is muted.
            var preferences = await preferencesRepository.GetByUserIdAsync(message.UserId, ct);
            var category = NotificationEventCatalog.GetCategoryFor(message.EventKey);

            if (preferences is not null && category.HasValue
                && !preferences.IsAllowed(category.Value))
            {
                logger.LogInformation(
                    "User {UserId} has muted category {Category} for event {EventKey} — skipping",
                    message.UserId, category.Value, message.EventKey);
                return;
            }

            var devices = await deviceRepository.GetByUserIdAsync(message.UserId, ct);
            var eligibleDevices = devices
                .Where(d => d.NotificationsEnabled && !string.IsNullOrEmpty(d.DeviceToken))
                .ToList();

            if (eligibleDevices.Count == 0)
            {
                logger.LogInformation(
                    "No eligible devices for user {UserId} on event {EventKey}",
                    message.UserId, message.EventKey);
                return;
            }

            var tokens = eligibleDevices.Select(d => d.DeviceToken).ToList();

            var result = await pushDispatcher.SendAsync(
                tokens,
                message.EventKey,
                message.Args ?? new Dictionary<string, string>(),
                ct);

            // FCM is DELIBERATELY unconfigured (dev / CI no-op). The dispatcher signals this DISTINCTLY
            // from an all-failed-transient outcome via result.Skipped, because the two share the same
            // (0, count, []) shape. A skipped dispatch can never succeed on retry until the secret is
            // provisioned, so ACK it (return) — throwing here would burn every transactional push through
            // maxDequeueCount into the poison queue, contradicting the documented no-op. The claim is
            // already committed, so a later config-fixed deploy will NOT re-send this one (accepted: the
            // user is re-notified by the next event).
            if (result.Skipped)
            {
                logger.LogInformation(
                    "FCM dispatch skipped (provider unconfigured) for event {EventKey} to user {UserId} — acking",
                    message.EventKey, message.UserId);
                return;
            }

            // BLIND-8 second defect — a transient FCM/init failure must NOT masquerade as "all-failed,
            // nothing pruned" and silently ack. When every token failed with no dead token to prune
            // (the shape FcmPushDispatcher returns on a broad-catch / cold-start init race — NOT the
            // skipped case handled above), treat it as TRANSIENT: throw so the queue redelivers (per
            // D3.3). The claim is already committed, so the redelivery short-circuits if FCM actually
            // delivered. Genuinely-invalid tokens still flow through the dead-token prune path below.
            if (eligibleDevices.Count > 0
                && result.SuccessCount == 0
                && result.FailureCount > 0
                && result.InvalidTokens.Count == 0)
            {
                throw new InvalidOperationException(
                    $"FCM dispatch for event {message.EventKey} reported all-failed with no prunable token " +
                    "(transient init/dispatch fault) — retrying via queue");
            }

            // Prune dead tokens. FCM rejected these as permanently invalid;
            // leaving them in the table means we re-attempt them on every
            // future event for this user.
            if (result.InvalidTokens.Count > 0)
            {
                var invalidSet = new HashSet<string>(result.InvalidTokens);
                foreach (var dead in eligibleDevices.Where(d => invalidSet.Contains(d.DeviceToken)))
                {
                    deviceRepository.Remove(dead);
                }
                await unitOfWork.CommitAsync(ct);
            }

            logger.LogInformation(
                "Push dispatch for {EventKey} to user {UserId}: {Success} succeeded, {Failure} failed, {Pruned} pruned",
                message.EventKey, message.UserId,
                result.SuccessCount, result.FailureCount, result.InvalidTokens.Count);
        }
        catch (Exception ex)
        {
            // ── ADR-0002 D3.3 — failure CLASSIFICATION phase 2: TRANSIENT/INFRA THROW ──
            // Everything reaching here is infra/transient (a DB read fault, the FCM network call, a
            // commit failure, the all-failed transient guard above) — the body already deserialized and
            // validated. Re-throw so the queue retries up to maxDequeueCount (host.json = 5), then moves
            // the message to notifications-dispatch-poison for durable recording. Acking a transient
            // fault here would silently drop recoverable work.
            // S6: the body deserialized and validated by this point — log the safe scalar correlation
            // keys (UserId/EventKey), never the raw queue body.
            logger.LogError(ex,
                "Transient/infra failure dispatching push notification — will retry via queue for user {UserId} on event {EventKey}",
                message.UserId, message.EventKey);
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// ADR-0002 D2.1a bare-body subject synthesis. The envelope's MessageKey is authoritative; only when
    /// a pre-envelope bare message is in-flight do we rebuild the key, taking the subject segment from the
    /// payload's <c>Args</c> in the order producers populate it (orderId, then disputeId, then
    /// membershipId). A subjectless event keeps the trailing separator (the frozen D2.1 shape).
    /// </summary>
    private static string SubjectFrom(SendPushNotificationMessage message)
    {
        if (message.Args is null) return string.Empty;
        foreach (var argKey in new[] { "orderId", "disputeId", "membershipId" })
        {
            if (message.Args.TryGetValue(argKey, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// ADR-0002 D2.1a dual-read. Returns the <see cref="SendPushNotificationMessage"/> payload, the
    /// envelope's TenantId, and the envelope's authoritative D2.1 MessageKey from either the
    /// <see cref="QueueEnvelope{T}"/> wire shape or the bare (pre-envelope) message (in which case the
    /// MessageKey is null and the consumer synthesizes it). Throws <see cref="JsonException"/> only when
    /// neither shape is parseable.
    /// </summary>
    private static (SendPushNotificationMessage? Message, string? EnvelopeTenantId, string? EnvelopeMessageKey) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendPushNotificationMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { UserId: not null } payload)
            {
                return (payload, envelope.TenantId, envelope.MessageKey);
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        return (JsonSerializer.Deserialize<SendPushNotificationMessage>(messageText, JsonOptions), null, null);
    }
}
