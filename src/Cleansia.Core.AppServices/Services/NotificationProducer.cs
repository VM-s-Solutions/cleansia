using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="INotificationProducer"/>
public class NotificationProducer(
    IUserNotificationRepository userNotificationRepository,
    IPendingDispatch pendingDispatch,
    IUserRepository userRepository,
    ILogger<NotificationProducer> logger) : INotificationProducer
{
    public async Task NotifyAsync(
        string userId,
        string eventKey,
        Dictionary<string, string> args,
        string? tenantId,
        string? subject,
        CancellationToken cancellationToken)
    {
        // Notification ownership follows the recipient, while the business action keeps its operator.
        var recipientTenantId = await userRepository.GetNotificationRecipientTenantAsync(userId, cancellationToken);
        if (string.IsNullOrEmpty(recipientTenantId))
        {
            logger.LogWarning("Notification {EventKey} skipped: recipient {UserId} has no persisted account company", eventKey, userId);
            return;
        }

        if (NotificationFeedEventKeys.IsFeedEvent(eventKey))
        {
            await UpsertFeedRowAsync(userId, eventKey, args, recipientTenantId, cancellationToken);
        }

        var messageKey = MessageKeys.Push(userId, eventKey, subject);
        pendingDispatch.Enqueue(
            QueueNames.NotificationsDispatch,
            new QueueEnvelope<SendPushNotificationMessage>(
                messageKey,
                recipientTenantId,
                new SendPushNotificationMessage(userId, eventKey, args, recipientTenantId)),
            messageKey);
    }

    private async Task UpsertFeedRowAsync(
        string userId,
        string eventKey,
        Dictionary<string, string> args,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        var argsJson = JsonSerializer.Serialize(args);

        if (NotificationEventCatalog.CollapsingDigestKeys.Contains(eventKey))
        {
            var unreadDigest = await userNotificationRepository
                .GetUnreadByUserAndEventAsync(userId, eventKey, cancellationToken);
            if (unreadDigest is not null)
            {
                unreadDigest.RefreshDigest(argsJson, DateTimeOffset.UtcNow);
                return;
            }
        }

        userNotificationRepository.Add(UserNotification.Create(userId, eventKey, argsJson, tenantId));
    }
}
