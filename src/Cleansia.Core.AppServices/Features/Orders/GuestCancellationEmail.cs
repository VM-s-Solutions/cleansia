using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells a guest their booking is off, whoever cancelled it. Every link the guest holds is retired here and
/// the send mints the one that still opens the booking, so the e-mail is the guest's only live key to what
/// happened and what came back. Staged into the caller's unit of work; an account's booking is left to its
/// own notifications.
/// </summary>
public static class GuestCancellationEmail
{
    /// <param name="languageCode">The guest's language, or null for the one the booking was made in.</param>
    public static async Task EnqueueAsync(
        Order order,
        string? languageCode,
        decimal? successfulRefundAmount,
        GuestOrderAccessTokenIssuer accessTokenIssuer,
        IPendingDispatch pending,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(order.UserId))
        {
            return;
        }

        await accessTokenIssuer.RevokeAsync(order, cancellationToken);
        var key = MessageKeys.GuestOrderCancelledEmail(order.Id);
        pending.Enqueue(QueueNames.SendEmail,
            new QueueEnvelope<SendGuestOrderCancellationEmailMessage>(key, order.TenantId,
                new SendGuestOrderCancellationEmailMessage(
                    order.Id, languageCode ?? EmailLocale.Resolve(order.LanguageCode), successfulRefundAmount, order.TenantId)),
            key);
    }
}
