using Stripe;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Handles the four Stripe subscription lifecycle events (created / updated
/// / deleted / invoice.payment_failed) by syncing the local
/// <c>UserMembership</c> row. Extracted from <c>HandlePaymentNotification</c>
/// so the order-payment and subscription-lifecycle paths stay independently
/// readable — they share only the webhook entry point.
/// </summary>
public interface IStripeSubscriptionWebhookHandler
{
    /// <summary>
    /// Apply the subscription event to the local <c>UserMembership</c>. A
    /// subscription it does not know, web Checkout or phone, gets its row from
    /// the first <c>customer.subscription.created</c> or <c>.updated</c> that
    /// says <c>active</c> or <c>trialing</c>; the phone's confirm usually makes
    /// it first, and this is its backstop.
    /// -> /flows/loyalty-and-memberships#plus-paid-before-granted
    /// Returns the Stripe subscription id (or empty string when ignored).
    /// </summary>
    Task<string> HandleAsync(Event stripeEvent, CancellationToken cancellationToken);
}
