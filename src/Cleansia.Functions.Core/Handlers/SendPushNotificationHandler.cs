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
/// Mirrors <c>GenerateReceiptHandler</c>: queue-trigger, no JWT context,
/// cross-tenant lookup with override on the <c>ITenantProvider</c>.
/// </summary>
public class SendPushNotificationHandler(
    IDeviceRepository deviceRepository,
    IUserNotificationPreferencesRepository preferencesRepository,
    IPushDispatcher pushDispatcher,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ILogger<SendPushNotificationHandler> logger)
{
    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        // ── ADR-0002 D3.3 (T-0120 AC4) — failure CLASSIFICATION, phase 1: PERMANENT failures ack ──
        // A malformed / un-deserializable body or a business-rejected message (missing required fields)
        // can NEVER succeed on retry. Previously the single throw-on-everything catch below burned all
        // 5 dequeues and then poison-queued an un-fixable message. Now a permanent failure is logged at
        // Warning and ACKED (return) — it never reaches the infra-touching work nor the transient catch.
        // ADR-0002 D2.1a — DUAL-READ at the deploy boundary. Producers wrap the payload in a
        // QueueEnvelope<T> ({"messageKey","tenantId","payload":{...}}); bare messages may still be
        // in-flight when this consumer deploys. Try the envelope first, fall back to the bare body.
        // The envelope's TenantId (when present) is authoritative for the override.
        SendPushNotificationMessage? message;
        string? envelopeTenantId;
        try
        {
            (message, envelopeTenantId) = ReadPayload(messageText);
        }
        catch (JsonException ex)
        {
            // Malformed body — permanent. Ack (do NOT throw → do NOT poison).
            logger.LogWarning(ex,
                "Discarding push message: malformed/un-deserializable body (permanent). Message: {Message}",
                messageText);
            return;
        }

        if (message is null)
        {
            logger.LogWarning(
                "Discarding push message: deserialized to null (permanent). Message: {Message}",
                messageText);
            return;
        }

        if (string.IsNullOrEmpty(message.UserId) || string.IsNullOrEmpty(message.EventKey))
        {
            logger.LogWarning(
                "Discarding push message with missing UserId or EventKey: {Message}",
                messageText);
            return;
        }

        try
        {
            // Cross-tenant lookup — the queue trigger has no JWT, so the EF
            // global filter has no tenant. Set the override from the envelope
            // (authoritative) or the payload so subsequent reads/writes attach
            // to the right tenant.
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
            // ── ADR-0002 D3.3 (T-0120 AC4) — failure CLASSIFICATION, phase 2: TRANSIENT/INFRA throw ──
            // Everything reaching here is infra/transient (a DB read fault, the FCM network call, a
            // commit failure) — the body already deserialized and validated above. Re-throw so the
            // Azure Functions queue trigger retries up to maxDequeueCount (host.json = 5) and then
            // moves the message to notifications-dispatch-poison, where NotificationsDispatchPoisonHandler
            // durably records it. Acking a transient fault here would silently drop recoverable work.
            logger.LogError(ex,
                "Transient/infra failure dispatching push notification — will retry via queue. Message: {Message}",
                messageText);
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// ADR-0002 D2.1a dual-read. Returns the <see cref="SendPushNotificationMessage"/> payload from
    /// either the new <see cref="QueueEnvelope{T}"/> wire shape (along with the envelope's TenantId) or
    /// the bare (pre-envelope) message. The payload is the discriminator: an envelope is only accepted
    /// when its payload carries a UserId, otherwise we fall back to the bare read. Throws
    /// <see cref="JsonException"/> only when neither shape is parseable.
    /// </summary>
    private static (SendPushNotificationMessage? Message, string? EnvelopeTenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendPushNotificationMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { UserId: not null } payload)
            {
                return (payload, envelope.TenantId);
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        return (JsonSerializer.Deserialize<SendPushNotificationMessage>(messageText, JsonOptions), null);
    }
}
