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
    /// Lookup by Stripe subscription id. Used by webhook handlers to reconcile
    /// state changes ("this subscription's status flipped — find the local row").
    /// Returns null if no local row matches (typically a webhook for a sub we
    /// never tracked, e.g. created out-of-band in Stripe Dashboard).
    /// </summary>
    Task<UserMembership?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// Has this user ever started a free trial, on any enrolment? The once-per-customer trial rule
    /// (owner ruling 2026-08-03) needs an answer that SURVIVES re-subscription, and a re-subscribe
    /// creates a new <see cref="UserMembership"/> row — so the fact cannot live on the current row. It
    /// lives in the user's row HISTORY: any row, any status, carrying a non-null
    /// <see cref="UserMembership.TrialEndsAtUtc"/>. Cancelled and expired rows are exactly the ones that
    /// matter here, and deactivated rows count too: soft-deleting a row does not un-grant the trial it
    /// recorded (the deliberate S10 exception).
    ///
    /// <para>Tenant-scoped: both callers are authenticated request paths. A background/webhook caller
    /// would get "no trial ever" for every tenanted row and hand out a second trial — if one is ever
    /// needed, it gets its own named IgnoringTenant variant rather than this one widening (S8).</para>
    /// </summary>
    Task<bool> HasEverStartedTrialAsync(string userId, CancellationToken cancellationToken);
}
