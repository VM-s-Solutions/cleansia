using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells the customer their booking stands and confirms the contract on a durable medium, a PDF attached,
/// at the moment the terms conclude it: a cash booking when it is made, a card booking once its payment
/// completes. A cash booking gets it in place of the receipt, which is issued at completion after the
/// cash was recorded (owner ruling 2026-09-28). Staged into the caller's unit of work, so it is sent only
/// for a booking that committed.
/// </summary>
public static class OrderBookedEmail
{
    public static void Enqueue(Order order, string languageCode, IPendingDispatch pending, DateTimeOffset contractConcludedOn)
    {
        var key = MessageKeys.OrderBookedEmail(order.Id);
        pending.Enqueue(QueueNames.SendEmail,
            new QueueEnvelope<SendOrderBookedEmailMessage>(key, order.TenantId,
                new SendOrderBookedEmailMessage(order.Id, languageCode, order.TenantId, contractConcludedOn)),
            key);
    }
}
