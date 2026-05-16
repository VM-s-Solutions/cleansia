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
    /// Apply the subscription event to the local <c>UserMembership</c>. Auto-
    /// provisions the row on a <c>customer.subscription.created</c> for web's
    /// Checkout flow (mobile creates the row inline before the webhook lands).
    /// Returns the Stripe subscription id (or empty string when ignored).
    /// </summary>
    Task<string> HandleAsync(Event stripeEvent, CancellationToken cancellationToken);
}
