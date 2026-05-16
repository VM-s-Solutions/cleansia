using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

/// <summary>
/// Consumes <see cref="SendPushNotificationMessage"/> from
/// <c>notifications-dispatch</c>, resolves the user's active devices that
/// allow this category, fans out via <see cref="IPushDispatcher"/>, and
/// prunes any <c>Device</c> rows whose tokens FCM rejected as permanently
/// invalid.
///
/// Mirrors <c>GenerateReceiptFunction</c>: queue-trigger, no JWT context,
/// cross-tenant lookup with override on the <c>ITenantProvider</c>.
/// </summary>
public class SendPushNotificationFunction(
    IDeviceRepository deviceRepository,
    IUserNotificationPreferencesRepository preferencesRepository,
    IPushDispatcher pushDispatcher,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ILogger<SendPushNotificationFunction> logger)
{
    [Function("SendPushNotification")]
    public async Task Run(
        [QueueTrigger("notifications-dispatch", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
    {
        SendPushNotificationMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<SendPushNotificationMessage>(
                messageText,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize SendPushNotificationMessage: {messageText}");

            if (string.IsNullOrEmpty(message.UserId) || string.IsNullOrEmpty(message.EventKey))
            {
                logger.LogWarning(
                    "Discarding push message with missing UserId or EventKey: {Message}",
                    messageText);
                return;
            }

            // Cross-tenant lookup — the queue trigger has no JWT, so the EF
            // global filter has no tenant. Set the override from the message
            // so subsequent reads/writes attach to the right tenant.
            if (!string.IsNullOrEmpty(message.TenantId))
            {
                tenantProvider.SetTenantOverride(message.TenantId);
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
            logger.LogError(ex,
                "Failed to dispatch push notification. Message: {Message}",
                messageText);
            throw; // Re-throw so Azure Functions retries via queue.
        }
    }
}
