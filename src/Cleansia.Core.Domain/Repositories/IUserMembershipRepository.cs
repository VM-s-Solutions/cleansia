using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Repository for <see cref="UserMembership"/> reads. Writes go through the
/// Stripe webhook handler which mutates the entity directly via the unit of
/// work — there's no business operation that creates memberships locally
/// without a corresponding Stripe subscription event, so write methods live
/// closer to the webhook code.
/// </summary>
public interface IUserMembershipRepository : IRepository<UserMembership, string>
{
    /// <summary>Latest authoritatively paid enrolment for a proven account owner, tracked for its lapse latch.</summary>
    Task<UserMembership?> GetLatestPaidForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's live enrolment — a subscription Stripe still holds open, paid up or not:
    /// Active, PastDue or Paused, inside its period, with
    /// <see cref="UserMembership.MembershipPlan"/> loaded. It is what refuses a second subscription, what
    /// the customer cancels, what the webhook reconciles against and what erasure cancels — so a past-due
    /// member, whose benefits have stopped, is still found here. Null when nothing is live.
    /// </summary>
    Task<UserMembership?> GetLifecycleForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// No-tracking variant of <see cref="GetLifecycleForUserAsync"/> for read-only callers
    /// (GetMyMembership, cancellation-policy resolution). Returns the SAME row + MembershipPlan as the
    /// tracked variant; it just doesn't enrol the entity in the change tracker. The tracked variant
    /// stays the one for load-then-mutate handlers (cancel/swap/webhook reconciliation).
    /// </summary>
    Task<UserMembership?> GetLifecycleForUserNoTrackingAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolve the membership that ENTITLES the user to Cleansia Plus benefits — a live enrolment in good
    /// standing: <see cref="MembershipStatus.Active"/> and inside its period, whether it is paid or inside
    /// its free trial. A trialing member gets every benefit a paying one does (owner ruling 2026-09-30).
    ///
    /// <para><b>This is deliberately a second method rather than a narrowing of
    /// <see cref="GetLifecycleForUserAsync"/>, and the distinction is load-bearing.</b> That one answers "is
    /// there a live enrolment?" and is what stops a second Stripe subscription being created, what lets a
    /// customer cancel, what the webhook reconciles against, and what GDPR erasure must see. A past-due or
    /// paused member is live there and entitled to nothing here. Narrowing the lifecycle read in place
    /// would make such a customer look unsubscribed to <c>CreateMembershipSubscription</c>, which would
    /// mint a SECOND subscription and collide with the filtered unique index on (TenantId, UserId) over the
    /// live statuses. Two questions, two methods.</para>
    /// </summary>
    Task<UserMembership?> GetEntitledForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>No-tracking variant of <see cref="GetEntitledForUserAsync"/>, for read-only callers.</summary>
    Task<UserMembership?> GetEntitledForUserNoTrackingAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lookup by Stripe subscription id. Used by webhook handlers to reconcile
    /// state changes ("this subscription's status flipped — find the local row").
    /// Returns null if no local row matches (typically a webhook for a sub we
    /// never tracked, e.g. created out-of-band in Stripe Dashboard).
    /// </summary>
    Task<UserMembership?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// Has this user ever started a trial, on any enrolment? The once-per-customer rule needs an answer
    /// that SURVIVES re-subscription, and a re-subscribe creates a new row — <b>so the fact cannot live on
    /// the current row.</b> It lives in the row history: any row, any status, carrying a trial end date.
    /// Deactivated rows count too — soft-deleting a row does not un-grant the trial it recorded.
    ///
    /// <para>Tenant-scoped: a background caller would get "no trial ever" for every tenanted row and hand
    /// out a second trial. → /flows/loyalty-and-memberships</para>
    /// </summary>
    Task<bool> HasEverStartedTrialAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Has this user ever held a membership billed in a currency other than <paramref name="currencyId"/>,
    /// in any status? The question behind adopting the legacy Stripe Customer for a currency: a Customer
    /// that ever invoiced another currency is locked to it by Stripe. Historical, so every row counts.
    /// </summary>
    Task<bool> HasAnyInOtherCurrencyAsync(string userId, string currencyId, CancellationToken cancellationToken);
}
