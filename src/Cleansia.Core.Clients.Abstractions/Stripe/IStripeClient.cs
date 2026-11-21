using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Clients.Abstractions.Stripe;

public interface IStripeClient
{
    /// <summary>
    /// Mint (or, inside Stripe's idempotency window, replay) the Checkout Session for an order.
    ///
    /// <para>Returns BOTH halves deliberately. The caller redirects the browser to
    /// <see cref="CheckoutSessionResult.Url"/>, but the refund path needs
    /// <see cref="CheckoutSessionResult.Id"/> — <see cref="RefundCheckoutSessionAsync"/> looks the
    /// session up by id, and this method used to discard it, so no web card order carried a charge
    /// surface at all and a refund fell through to a null PaymentIntent.</para>
    ///
    /// <para>An order that already records a session gets that session back while Stripe still has it
    /// open, so a resumed checkout is the one the customer started, whichever parameters it was opened
    /// with — a card-saving session included.</para>
    /// </summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// The booking's Checkout Session for a customer who ticked "save this card": the same session, opened
    /// on <paramref name="stripeCustomerId"/> with <c>setup_future_usage=off_session</c>, so Stripe keeps
    /// the card for off-session use. The session and its PaymentIntent carry <c>SavedCardId</c> metadata,
    /// which is how the payment webhook finds the row the card lands on. Keyed on the order like the plain
    /// session; an order is opened one way or the other, never both.
    /// </summary>
    Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(
        Order order, string stripeCustomerId, string savedCardId, CancellationToken cancellationToken);

    /// <summary>
    /// The same Checkout Session, closing at <paramref name="expiresAtUtc"/> instead of Stripe's default
    /// 24 hours, for an order that must not stay payable past a moment of its own. The expiry is part of
    /// the idempotency key: the same expiry replays the same session, and a later one opens the next.
    /// Stripe refuses an expiry under 30 minutes or over 24 hours away. Backing out returns the customer to
    /// the order's own page, not to the booking cancel page, whose resume asks without the expiry and so
    /// would open a second session beside this one.
    /// <para>A session the order records that is still open is handed back while it asks for the same
    /// amount and keeps the card exactly when this call does. Otherwise it is expired before the new one
    /// opens, keyed on the one it replaces, so the order never has two sessions open.</para>
    /// </summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Order order, DateTime expiresAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The expiring Checkout Session for a customer who ticked "save this card": opened on
    /// <paramref name="stripeCustomerId"/> with <c>setup_future_usage=off_session</c> and <c>SavedCardId</c>
    /// metadata, as <see cref="CreateCardSavingCheckoutSessionAsync(Order, string, string, CancellationToken)"/>
    /// opens the booking's, and expiring and handed back as the plain expiring session is.
    /// </summary>
    Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(
        Order order, DateTime expiresAtUtc, string stripeCustomerId, string savedCardId, CancellationToken cancellationToken);

    /// <summary>
    /// Refund a previously-paid checkout session. Amount is in the session's currency. Returns the id of
    /// the refund Stripe created (or replayed on the same key).
    /// <para>
    /// <paramref name="idempotencyKey"/> <b>MUST be the caller's deterministic refund key (ADR-0006 D3),
    /// never a per-call Guid or timestamp</b> — the same key replays the same refund instead of issuing a
    /// second one, which is the only reason this write may be auto-retried at all.
    /// → /flows/cancellation-refund-dispute#refund
    /// </para>
    /// </summary>
    Task<string> RefundCheckoutSessionAsync(
        string stripeSessionId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Refund a charge captured on a PaymentIntent directly (the mobile PaymentSheet path, where there
    /// is no Checkout Session — T-0347 suppresses it). Amount is in the intent's currency. Returns the id
    /// of the refund Stripe created (or replayed on the same key).
    /// <para>
    /// <paramref name="idempotencyKey"/> — same contract as <see cref="RefundCheckoutSessionAsync"/>:
    /// <b>deterministic, never per-call</b>. → /flows/cancellation-refund-dispute#refund
    /// </para>
    /// </summary>
    Task<string> RefundPaymentIntentAsync(
        string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// The refund Stripe made on <paramref name="refundKey"/>, found among the refunds of the order's
    /// PaymentIntent by the <c>RefundKey</c> metadata both refund calls above tag it with; null when Stripe
    /// has none. A live refund is preferred over a failed or canceled one under the same key. Read-only.
    /// <para><b>An unreachable Stripe THROWS and is never reported as "no refund"</b>, so a retry that must
    /// not pay twice sends nothing. → /flows/cancellation-refund-dispute#refund</para>
    /// </summary>
    Task<StripeRefundSnapshot?> FindRefundAsync(
        string? stripeSessionId,
        string? stripePaymentIntentId,
        string refundKey,
        CancellationToken cancellationToken);

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
    /// <para>Stripe keeps the card only when <paramref name="savedCardId"/> is given: the intent then asks
    /// for <c>setup_future_usage=off_session</c> and carries <c>SavedCardId</c> metadata for the webhook.
    /// Without it nothing is kept.</para>
    /// <para><paramref name="currentPaymentIntentId"/> is the intent the order already records. It is handed
    /// back while the customer can still confirm it, for the same amount, on the same Stripe Customer, and
    /// keeping the card exactly when <paramref name="savedCardId"/> is given (on the saved card it was opened
    /// with). Otherwise a new intent is created, keyed on the one it replaces, so Stripe can never replay an
    /// intent the order has moved past; the caller cancels the replaced one.</para>
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string stripeCustomerId,
        string orderId,
        string displayOrderNumber,
        string? savedCardId,
        string? currentPaymentIntentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancel a previously-created PaymentIntent that hasn't been confirmed yet.
    /// Safe to call on an intent in <c>requires_payment_method</c>
    /// / <c>requires_confirmation</c> / <c>processing</c> states. Throws if the
    /// intent has already succeeded.
    /// </summary>
    Task CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancel the PaymentSheet intent a newer one for the same order replaces, with the reason
    /// <c>duplicate</c>: the <c>payment_intent.canceled</c> it raises leaves the order alone, because the
    /// customer is about to pay the newer one. Throws if the intent has already succeeded.
    /// </summary>
    Task CancelReplacedPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read the LIVE settlement state of an order's card charge. Read-only. Callers use it to answer
    /// "has this customer already paid?" before taking an alternative tender, so <b>the answer must come
    /// from Stripe rather than our possibly-stale <c>PaymentStatus</c></b> — the whole point is that the
    /// webhook may never have arrived.
    /// <para><b>An unreachable Stripe THROWS and is never reported as "unpaid"</b>, so a caller that must
    /// not double-charge fails closed. → /flows/payment-and-fiscal</para>
    /// </summary>
    Task<StripePaymentSnapshot> GetPaymentSnapshotAsync(
        string? stripeSessionId,
        string? stripePaymentIntentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The <c>OrderId</c> metadata of the Checkout Session that charged <paramref name="paymentIntentId"/>,
    /// or null when no Checkout Session did (a mobile PaymentSheet intent, a subscription invoice).
    /// Read-only; an unreachable Stripe throws.
    /// </summary>
    Task<string?> FindCheckoutSessionOrderIdAsync(
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
    /// A card-only SetupIntent that saves a card on <paramref name="stripeCustomerId"/> for later
    /// off-session use, confirmed by the mobile PaymentSheet in setup mode. It charges nothing. The
    /// intent carries <paramref name="savedCardId"/> as <c>SavedCardId</c> metadata, which is how the
    /// <c>setup_intent.succeeded</c> webhook finds the row the card lands on.
    /// </summary>
    Task<SetupIntentResult> CreateCardSetupIntentAsync(
        string stripeCustomerId,
        string savedCardId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The web twin of <see cref="CreateCardSetupIntentAsync"/>: a Checkout Session in setup mode,
    /// card only, on <paramref name="stripeCustomerId"/>. It charges nothing. The session and the
    /// SetupIntent it creates both carry <c>SavedCardId</c> metadata. Returns the URL the browser is
    /// sent to; the return URLs are derived from <c>Stripe:SuccessUrlBase</c>, never taken from a caller.
    /// </summary>
    Task<string> CreateCardSetupCheckoutSessionAsync(
        string stripeCustomerId,
        string savedCardId,
        CancellationToken cancellationToken);

    /// <summary>
    /// A SetupIntent's Customer, the saved-card row its <c>SavedCardId</c> metadata names, and the card it saved,
    /// which is null when the intent has not succeeded or saved no card. Read-only; an unreachable Stripe throws.
    /// </summary>
    Task<SetupIntentCard> GetSetupIntentCardAsync(
        string setupIntentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The card a payment kept for off-session use, or null when the PaymentIntent has not succeeded, did
    /// not ask Stripe to keep its card, or was paid with no card. Read-only; an unreachable Stripe throws.
    /// </summary>
    Task<SavedCardDetails?> GetPaymentIntentCardAsync(
        string paymentIntentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a removed saved card off <paramref name="stripeCustomerId"/>, so no later sheet on that Customer
    /// offers it. The Customer is the one per currency that Cleansia Plus also bills, so a card a subscription
    /// not yet cancelled, or the Customer's invoices, pay with stays on it. A card already off the Customer is
    /// left alone. An unreachable Stripe throws.
    /// </summary>
    Task DetachSavedCardAsync(
        string stripeCustomerId,
        string paymentMethodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Charge a customer's saved card, with the customer absent, for what they owe on a receivable: a
    /// PaymentIntent created with <c>off_session</c> and <c>confirm</c>, carrying <c>ReceivableId</c>
    /// metadata so the <c>payment_intent.*</c> webhooks find it. Returns the intent's id. A decline, or a
    /// bank that asks for the customer to authenticate, throws a <c>StripeException</c> of type
    /// <c>card_error</c>, and Stripe then sends <c>payment_intent.payment_failed</c>.
    /// <paramref name="attempt"/> is the receivable's attempt number, so a retry of one attempt replays it.
    /// </summary>
    Task<string> ChargeReceivableOffSessionAsync(
        string receivableId,
        decimal amount,
        string currency,
        string stripeCustomerId,
        string paymentMethodId,
        int attempt,
        CancellationToken cancellationToken);

    /// <summary>
    /// The pay link for a receivable: a payment-mode Checkout Session for its amount, card only, returning
    /// to the order's page. The <c>ReceivableId</c> metadata is on the session and deliberately not on its
    /// PaymentIntent, so only <c>checkout.session.completed</c> settles it and a card the customer mistypes
    /// on the page is not reported as a failed off-session charge. <paramref name="currentSessionId"/> is
    /// the receivable's latest link: it is handed back while Stripe still has it open, so a link e-mailed
    /// and a link opened in the app are the same one, and otherwise a new session is opened, keyed on the
    /// one it replaces. It is never keyed on the receivable alone, because Stripe replays a key's first
    /// answer for a day or more, and that answer is by then an expired session.
    /// </summary>
    Task<CheckoutSessionResult> CreateReceivableCheckoutSessionAsync(
        string receivableId,
        string? currentSessionId,
        string orderId,
        string displayOrderNumber,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes a receivable's pay link before its off-session charge, so the customer cannot pay it twice.
    /// Returns false when the customer has already paid through the link, and the charge must not be
    /// taken; a link that expired on its own is closed already. An unreachable Stripe, or a customer who
    /// pays in the instant between the read and the close, throws.
    /// </summary>
    Task<bool> ExpireReceivableCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a Stripe subscription against a price, after the phone's Plus sheet confirmed the SetupIntent of
    /// <see cref="CreateSetupIntentAsync"/>: the card of the newest such intent that succeeded on the Customer,
    /// one with no <c>SavedCardId</c> metadata, is the subscription's default payment method, so the invoice
    /// after the trial (<paramref name="trialPeriodDays"/> &gt; 0) and every renewal charge it. The Customer's
    /// own invoice default is left alone. The subscription carries the same <c>UserId</c> and
    /// <c>MembershipPlanCode</c> metadata as a web Checkout one, which is what the subscription webhooks
    /// provision a membership from.
    /// <para>With no trial Stripe charges the first invoice at once with that card, and the result says how it
    /// went: <see cref="SubscriptionResult.Status"/> <c>active</c> when it is paid; otherwise
    /// <see cref="SubscriptionResult.PaymentIntentClientSecret"/> when the card needs 3-D Secure, or
    /// <see cref="SubscriptionResult.DeclineCode"/> when it was declined. A retry on the same attempt id replays the
    /// same subscription and reads its state afresh, so the call after a 3-D Secure step finds it paid.</para>
    /// <para>Before creating, it reads the Customer's subscriptions that carry this <paramref name="userId"/>: one
    /// already paid or trialing is returned and nothing is created, so a new attempt after a paid one whose confirm
    /// never landed charges nothing more; one still unpaid that this attempt does not replay is cancelled after the
    /// create.</para>
    /// </summary>
    Task<SubscriptionResult> CreateSubscriptionAsync(
        string stripeCustomerId,
        string stripePriceId,
        string userId,
        string membershipPlanCode,
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
    /// Cancel a subscription immediately and void its open invoice, so nothing more is charged. For an
    /// enrolment with no paid period to run out — past due or paused — where cancelling at period end
    /// would leave Stripe retrying the card until then.
    /// </summary>
    Task CancelSubscriptionNowAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a Stripe Checkout Session in subscription mode for the web
    /// customer flow. The customer is redirected to Stripe-hosted Checkout,
    /// completes payment, and returns to the membership welcome page. The first
    /// <c>customer.subscription.created</c> or <c>.updated</c> webhook that says
    /// <c>active</c> or <c>trialing</c> is what creates the local
    /// <see cref="Cleansia.Core.Domain.Memberships.UserMembership"/> row —
    /// success-url polling is not required.
    /// -> /flows/loyalty-and-memberships#plus-paid-before-granted
    /// <para>
    /// The return URLs are NOT parameters. They used to be, taken from the client and passed
    /// through unchecked, which let any authenticated caller choose where Stripe sent a browser
    /// after payment. They are now derived from <c>Stripe:SuccessUrlBase</c> — the same authority
    /// the order flow has always used, one method up in this same interface.
    /// </para>
    /// </summary>
    Task<string> CreateMembershipCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        string userId,
        string membershipPlanCode,
        int trialPeriodDays,
        string idempotencyAttemptId,
        CancellationToken cancellationToken);
}

/// <summary>
/// What Stripe currently says about a charge surface. Deliberately coarse — a caller deciding whether
/// it is safe to take a second tender only needs "money settled" / "money in flight" / "no money".
/// </summary>
public enum StripePaymentState
{
    /// <summary>No charge has settled and none is in flight — the surface can be abandoned safely.</summary>
    Unpaid = 0,

    /// <summary>
    /// A charge is in flight (authorized-not-captured, async settlement, or the customer is mid-3DS).
    /// It may settle at any moment, so it must be treated as "may already be paid".
    /// </summary>
    Processing = 1,

    /// <summary>Stripe holds a settled charge for this surface — the customer has paid.</summary>
    Settled = 2,
}

/// <summary>
/// Result of <see cref="IStripeClient.GetPaymentSnapshotAsync"/>.
/// <paramref name="OutstandingPaymentIntentId"/> is the intent a caller may cancel to close the
/// surface; null when there is nothing cancellable (no intent, or it is already canceled/settled).
/// </summary>
public record StripePaymentSnapshot(StripePaymentState State, string? OutstandingPaymentIntentId);

/// <summary>
/// Result of <see cref="IStripeClient.FindRefundAsync"/>: the refund Stripe made, the amount it pays, and
/// whether Stripe failed or canceled it.
/// </summary>
public record StripeRefundSnapshot(string Id, decimal Amount, bool Failed);

/// <summary>
/// Result of <see cref="IStripeClient.CreateSetupIntentAsync"/>. The
/// ClientSecret is what the mobile/web SDK confirms against to attach a
/// payment method; the Id is opaque to us.
/// </summary>
public record SetupIntentResult(string Id, string ClientSecret);

/// <summary>Result of <see cref="IStripeClient.GetSetupIntentCardAsync"/>: the saved card, as Stripe describes it.</summary>
public record SavedCardDetails(string PaymentMethodId, string Brand, string Last4, int ExpMonth, int ExpYear);

/// <summary>Result of <see cref="IStripeClient.GetSetupIntentCardAsync"/>.</summary>
public record SetupIntentCard(string? CustomerId, string? SavedCardId, SavedCardDetails? Card);

/// <summary>
/// Snapshot of a freshly-created subscription. Period bounds are mirrored
/// into <see cref="Cleansia.Core.Domain.Memberships.UserMembership"/> so the
/// pricing pipeline can read membership status without a Stripe round-trip
/// on every order.
/// </summary>
public record SubscriptionResult(
    string SubscriptionId,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    /// <summary>
    /// Stripe's <c>trial_end</c>, mirrored onto <c>UserMembership.TrialEndsAtUtc</c>. Null = Stripe
    /// granted no trial on this subscription. It is what the once-per-customer trial rule reads back
    /// on the NEXT subscribe, so it is the answer Stripe gave, never the number we asked for.
    /// </summary>
    DateTime? TrialEnd = null,
    string? Status = null,
    /// <summary>The client secret of the first invoice's PaymentIntent while the card needs 3-D Secure; null otherwise.</summary>
    string? PaymentIntentClientSecret = null,
    /// <summary>Stripe's reason the first invoice's card was refused (its <c>decline_code</c>, else its error <c>code</c>); null otherwise.</summary>
    string? DeclineCode = null);

/// <summary>
/// Result of <see cref="IStripeClient.CreatePaymentIntentAsync"/>. The
/// ClientSecret is what the mobile SDK confirms against; the Id is the
/// canonical Stripe reference we persist on the Order for webhook reconciliation.
/// </summary>
/// <summary>
/// A Checkout Session's two halves: the <paramref name="Id"/> that identifies it to Stripe (and so
/// to the refund path) and the <paramref name="Url"/> the browser is sent to.
/// </summary>
public record CheckoutSessionResult(string Id, string Url);

public record PaymentIntentResult(string Id, string ClientSecret);