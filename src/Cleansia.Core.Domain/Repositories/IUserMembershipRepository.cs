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
    /// <summary>
    /// Resolve the user's currently-providing-benefits membership, with
    /// <see cref="UserMembership.MembershipPlan"/> eagerly loaded so the
    /// pricing pipeline can read DiscountPercentage / FreeCancellationWindowHours
    /// without a second round-trip. Returns null when the user has no active
    /// membership (or no memberships at all).
    /// </summary>
    Task<UserMembership?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// No-tracking variant of <see cref="GetActiveForUserAsync"/> for read-only callers
    /// (GetMyMembership, cancellation-policy resolution). Returns the SAME row + MembershipPlan as the
    /// tracked variant; it just doesn't enrol the entity in the change tracker. The tracked variant
    /// stays the one for load-then-mutate handlers (cancel/swap/webhook reconciliation).
    /// </summary>
    Task<UserMembership?> GetActiveForUserNoTrackingAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolve the membership that ENTITLES the user to Cleansia Plus benefits — a live enrolment that is
    /// also being paid for. Owner ruling 2026-09-08 (T-0690): no Plus benefit is granted until the
    /// customer actually subscribes.
    ///
    /// <para><b>This is deliberately a second method rather than a narrowing of
    /// <see cref="GetActiveForUserAsync"/>, and the distinction is load-bearing.</b> That one answers "is
    /// there a live enrolment?" and is what stops a second Stripe subscription being created, what lets a
    /// customer cancel, what the webhook reconciles against, and what GDPR erasure must see. Narrowing it
    /// in place would make a trialing customer look unsubscribed to <c>CreateMembershipSubscription</c>,
    /// which would mint a SECOND subscription and collide with the filtered unique index on
    /// (TenantId, UserId) WHERE Status = Active — a 500 on a paying customer. It would also refuse to
    /// cancel a live trial. Two questions, two methods.</para>
    ///
    /// <para>With the trial removed this is a backstop rather than a live gate: no new enrolment can be
    /// trialing, because both admin plan commands refuse a non-zero trial period. It stays because
    /// <c>TrialEndsAtUtc</c> is never cleared once set, historical rows may carry one, and a trial
    /// reintroduced by any route must not silently start granting benefits again.</para>
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
}
