using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="INotificationProducer"/>
public class NotificationProducer(
    IUserNotificationRepository userNotificationRepository,
    IPendingDispatch pendingDispatch) : INotificationProducer
{
    public async Task NotifyAsync(
        string userId,
        string eventKey,
        Dictionary<string, string> args,
        string? tenantId,
        string? subject,
        CancellationToken cancellationToken)
    {
        if (NotificationFeedEventKeys.IsFeedEvent(eventKey))
        {
            await UpsertFeedRowAsync(userId, eventKey, args, tenantId, cancellationToken);
        }

        var messageKey = MessageKeys.Push(userId, eventKey, subject);
        pendingDispatch.Enqueue(
            QueueNames.NotificationsDispatch,
            new QueueEnvelope<SendPushNotificationMessage>(
                messageKey,
                tenantId,
                new SendPushNotificationMessage(userId, eventKey, args, tenantId)),
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

        if (eventKey == NotificationEventCatalog.NewJobsAvailable)
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
