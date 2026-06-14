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
}
