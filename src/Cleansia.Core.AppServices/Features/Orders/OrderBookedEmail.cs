using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells the customer their cash booking stands: what to pay the cleaner, when and where, and the
/// cancellation rule. It takes the place of the receipt a cash booking used to get at once, which is now
/// issued at completion, after the cash was recorded (owner ruling 2026-09-28). Staged into the caller's
/// unit of work, so it is sent only for a booking that committed.
/// </summary>
public static class OrderBookedEmail
{
    public static void Enqueue(Order order, string languageCode, IPendingDispatch pending)
    {
        var key = MessageKeys.OrderBookedEmail(order.Id);
        pending.Enqueue(QueueNames.SendEmail,
            new QueueEnvelope<SendOrderBookedEmailMessage>(key, order.TenantId,
                new SendOrderBookedEmailMessage(order.Id, languageCode, order.TenantId)),
            key);
    }
}
