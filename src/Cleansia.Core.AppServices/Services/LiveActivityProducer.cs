using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="ILiveActivityProducer"/>
public class LiveActivityProducer(
    ILiveActivityTokenRepository liveActivityTokenRepository,
    IPendingDispatch pendingDispatch) : ILiveActivityProducer
{
    public async Task NotifyOrderTransitionAsync(Order order, string eventKey, OrderStatusTrack transition, CancellationToken cancellationToken)
    {
        // Guest orders carry no user, so they can hold no activity token — never a token lookup.
        if (string.IsNullOrEmpty(order.UserId))
        {
            return;
        }

        // The gate: no registered token for this order's activity ⇒ produce nothing (the vast majority
        // of orders — Android, web, or an iOS install that never registered). The gate lives in the
        // producer, not the handlers, so no status handler branches on token existence.
        var hasTokens = await liveActivityTokenRepository.HasTokensForOrderAsync(order.UserId, order.Id, cancellationToken);
        if (!hasTokens)
        {
            return;
        }

        var scheduledStart = new DateTimeOffset(DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc));
        var scheduledEnd = scheduledStart.AddMinutes(order.EstimatedTime);
        var messageKey = MessageKeys.LiveActivity(order.Id, eventKey, transition.Sequence);

        pendingDispatch.Enqueue(
            QueueNames.LiveActivityDispatch,
            new QueueEnvelope<SendLiveActivityUpdateMessage>(
                messageKey,
                order.TenantId,
                new SendLiveActivityUpdateMessage(
                    order.UserId,
                    order.Id,
                    eventKey,
                    order.DisplayOrderNumber,
                    scheduledStart,
                    scheduledEnd,
                    transition.CreatedOn,
                    order.TenantId)),
            messageKey);
    }
}
