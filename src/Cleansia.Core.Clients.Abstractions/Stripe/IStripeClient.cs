using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Clients.Abstractions.Stripe;

public interface IStripeClient
{
    Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Refund a previously-paid checkout session. Amount is in the session's currency.
    /// Used by customer cancellation flow when a partial or full refund is owed per policy.
    /// </summary>
    Task RefundCheckoutSessionAsync(string stripeSessionId, decimal amount, CancellationToken cancellationToken);

    /// <summary>
    /// Create a new Stripe Customer record for a user who's making their first
    /// card payment. Caller is responsible for persisting the returned id on the
    /// User entity and reusing it on subsequent calls — this method does NOT
    /// perform Stripe-side dedup.
    /// </summary>
    Task<string> CreateCustomerAsync(
        string email,
        string fullName,
        string? phone,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a PaymentIntent for an existing order. Used by the mobile
    /// PaymentSheet flow. Returns the intent id and the client_secret the
    /// mobile SDK needs to confirm payment.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string stripeCustomerId,
        string orderId,
        string displayOrderNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a short-lived Stripe ephemeral key tied to a customer. The mobile
    /// PaymentSheet uses this to display saved cards without requiring a full
    /// authentication round-trip. Lifetime is ~10 minutes — generate per
    /// PaymentSheet open.
    /// </summary>
    Task<string> CreateEphemeralKeyAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a SetupIntent so the user can attach a default payment method
    /// to their Stripe customer before we create a subscription. The
    /// PaymentSheet (in setup mode) confirms this; the resulting payment
    /// method becomes the default for off-session subscription invoices.
    /// </summary>
    Task<SetupIntentResult> CreateSetupIntentAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a Stripe subscription against a price. Caller is expected to
    /// have already attached a payment method via SetupIntent — first invoice
    /// is charged immediately (or after the trial if <paramref name="trialPeriodDays"/>
    /// > 0). Returns the subscription id + period bounds the caller persists
    /// in <see cref="Cleansia.Core.Domain.Memberships.UserMembership"/>.
    /// </summary>
    Task<SubscriptionResult> CreateSubscriptionAsync(
        string stripeCustomerId,
        string stripePriceId,
        int trialPeriodDays,
        CancellationToken cancellationToken);

    /// <summary>
    /// Swap an existing subscription to a different price (typically the
    /// monthly → yearly upgrade). Stripe prorates the cost difference and
    /// charges/credits the customer's default payment method on the swap.
    /// Returns the new period bounds — caller mirrors them into
    /// <see cref="Cleansia.Core.Domain.Memberships.UserMembership"/> so the
    /// management UI shows the correct renewal date right after the swap.
    /// </summary>
    Task<SubscriptionResult> SwapSubscriptionPriceAsync(
        string stripeSubscriptionId,
        string newStripePriceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Mark a subscription to cancel at the end of the current billing
    /// period. Stripe keeps it active through period end, then transitions
    /// to canceled and fires a webhook. The local UserMembership row stays
    /// IsActive until that webhook lands.
    /// </summary>
    Task CancelSubscriptionAtPeriodEndAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a Stripe Checkout Session in subscription mode for the web
    /// customer flow. The customer is redirected to Stripe-hosted Checkout,
    /// completes payment, and returns to <c>SuccessUrl</c>. The
    /// <c>customer.subscription.created</c> webhook is what creates the local
    /// <see cref="Cleansia.Core.Domain.Memberships.UserMembership"/> row —
    /// success-url polling is not required.
    /// </summary>
    Task<string> CreateMembershipCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        string userId,
        string membershipPlanCode,
        int trialPeriodDays,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of <see cref="IStripeClient.CreateSetupIntentAsync"/>. The
/// ClientSecret is what the mobile/web SDK confirms against to attach a
/// payment method; the Id is opaque to us.
/// </summary>
public record SetupIntentResult(string Id, string ClientSecret);

/// <summary>
/// Snapshot of a freshly-created subscription. Period bounds are mirrored
/// into <see cref="Cleansia.Core.Domain.Memberships.UserMembership"/> so the
/// pricing pipeline can read membership status without a Stripe round-trip
/// on every order.
/// </summary>
public record SubscriptionResult(
    string SubscriptionId,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd);

/// <summary>
/// Result of <see cref="IStripeClient.CreatePaymentIntentAsync"/>. The
/// ClientSecret is what the mobile SDK confirms against; the Id is the
/// canonical Stripe reference we persist on the Order for webhook reconciliation.
/// </summary>
public record PaymentIntentResult(string Id, string ClientSecret);