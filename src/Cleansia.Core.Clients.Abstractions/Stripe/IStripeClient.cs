using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Clients.Abstractions.Stripe;

public interface IStripeClient
{
    Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Refund a previously-paid checkout session. Amount is in the session's currency.
    /// <para>
    /// <paramref name="idempotencyKey"/> is passed straight to Stripe as the refund request's
    /// IdempotencyKey. It MUST be the caller's deterministic refund key (ADR-0006 D3), never a
    /// per-call Guid/timestamp: the same key replays the same Stripe refund instead of issuing a
    /// second one, which is what makes the ADR-0005 D1.2 resilience retry safe to auto-retry this
    /// write at all (an unkeyed write is never auto-retried).
    /// </para>
    /// </summary>
    Task RefundCheckoutSessionAsync(
        string stripeSessionId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Refund a charge captured on a PaymentIntent directly (the mobile PaymentSheet path, where there
    /// is no Checkout Session — T-0347 suppresses it). Amount is in the intent's currency.
    /// <para>
    /// <paramref name="idempotencyKey"/> is the caller's deterministic refund key (ADR-0006 D3), passed
    /// to Stripe verbatim — exactly as <see cref="RefundCheckoutSessionAsync"/> uses it: the same key
    /// replays the same Stripe refund instead of issuing a second one, which is what makes the resilience
    /// retry safe to auto-retry this write.
    /// </para>
    /// </summary>
    Task RefundPaymentIntentAsync(
        string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Create a new Stripe Customer record for a user who's making their first
    /// card payment. Caller is responsible for persisting the returned id on the
    /// User entity and reusing it on subsequent calls — this method does NOT
    /// perform Stripe-side dedup.
    /// </summary>
    Task<string> CreateCustomerAsync(
        string userId,
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
    /// Cancel a previously-created PaymentIntent that hasn't been confirmed yet.
    /// Used when the order's amount changes mid-flow and a new intent is being
    /// minted — the old intent must be cancelled so the customer can't end up
    /// paying both. Safe to call on an intent in <c>requires_payment_method</c>
    /// / <c>requires_confirmation</c> / <c>processing</c> states. Throws if the
    /// intent has already succeeded.
    /// </summary>
    Task CancelPaymentIntentAsync(
        string paymentIntentId,
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
        string idempotencyAttemptId,
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
        string idempotencyAttemptId,
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
        string idempotencyAttemptId,
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