using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Result of <see cref="ILoyaltyService.ResolveTierDiscountForOrderAsync"/>.
/// <see cref="DiscountAmount"/> is the CZK value (not %) to subtract from the
/// order total. <see cref="TierAtPurchase"/> is the user's tier at the
/// resolution moment, persisted on the order even when the discount itself
/// is zero (e.g. Silver below the 1000 CZK floor).
/// </summary>
public record TierDiscountResult(decimal DiscountAmount, LoyaltyTier? TierAtPurchase);

public interface ILoyaltyService
{
    /// <summary>
    /// Idempotent grant of <c>floor(order.TotalPrice / 10)</c> tier-points
    /// for a completed order. No-op if the user is anonymous, the points
    /// would be zero, or a prior earn ledger entry exists for this order.
    /// Called from <c>CompleteOrder.Handler</c>.
    /// </summary>
    Task GrantForCompletedOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Mirror of <see cref="GrantForCompletedOrderAsync"/> — if a prior
    /// earn entry exists for the order, append a negative-points
    /// revoke ledger entry. Otherwise no-op. Called from
    /// <c>CancelOrder.Handler</c>.
    /// </summary>
    Task RevokeForCancelledOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Compute the tier discount (CZK amount, not %) for a user + total.
    /// Returns (0, null) for anonymous users with no account, (0, tier)
    /// when the tier qualifies but the order is below the per-tier minimum,
    /// (discount, tier) otherwise. Called from <c>CreateOrder.Handler</c>.
    /// </summary>
    Task<TierDiscountResult> ResolveTierDiscountForOrderAsync(
        string userId, decimal orderTotal, CancellationToken cancellationToken);

    /// <summary>
    /// Grant a fixed points award outside the order-completion path (e.g.
    /// referral rewards, manual admin grants). The user's loyalty account is
    /// lazily created if missing.
    /// <para>
    /// Idempotency (S7a): the admin manual path supplies a
    /// REQUIRED client-generated <paramref name="requestId"/> which is persisted
    /// as the ledger row's idempotency key. A double-submit / retry collapses
    /// onto exactly one ledger row via a fast-path lookup-by-key AND the
    /// filtered unique-index backstop (Postgres 23505 caught and resolved to the
    /// same success). The order-driven / referral path passes
    /// <c>requestId: null</c> and remains keyed on
    /// (<paramref name="orderId"/>, <paramref name="source"/>) as before.
    /// </para>
    /// </summary>
    Task GrantPointsManuallyAsync(
        string userId,
        int points,
        LoyaltyEarnSource source,
        string? orderId,
        string actorId,
        string? requestId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Mirror of <see cref="GrantPointsManuallyAsync"/> for admin-driven
    /// revocations. Inserts a negative-points ledger row via
    /// <c>LoyaltyAccount.RevokePoints</c>. No-op when the account doesn't
    /// exist (nothing to revoke). Idempotency (S7a): keyed on the
    /// REQUIRED client-generated <paramref name="requestId"/> for the admin
    /// manual path (collapses a retry to one negative row); the order-driven
    /// path passes <c>requestId: null</c> and stays keyed on
    /// (<paramref name="orderId"/>, <paramref name="source"/>).
    /// </summary>
    Task RevokePointsManuallyAsync(
        string userId,
        int points,
        LoyaltyEarnSource source,
        string? orderId,
        string actorId,
        string? requestId,
        CancellationToken cancellationToken);
}
