using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Result of <see cref="ILoyaltyService.ResolveTierDiscountForOrderAsync"/>. <see cref="DiscountAmount"/>
/// is an amount in the ORDER's currency to subtract from the raw subtotal. <see cref="TierAtPurchase"/>
/// is the user's tier at the resolution moment, persisted on the order even when the discount is zero.
/// <see cref="MinimumOrderAmount"/> is the floor that was JUDGED — null when the tier has none, or when
/// the order is not in the platform default currency, where the floor is not applied at all
/// (→ /product/business-rules#money-constants) — so the quote states exactly the rule the order used.
/// </summary>
public record TierDiscountResult(
    decimal DiscountAmount,
    LoyaltyTier? TierAtPurchase,
    decimal? MinimumOrderAmount = null);

public interface ILoyaltyService
{
    /// <summary>
    /// Idempotent grant of <c>floor(order.TotalPrice / Currency.LoyaltyPointsDivisor)</c> tier-points
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
    /// The loyalty clawback for one refund of a completed order — partial, full or a dispute's
    /// settlement: revokes the same share of the order's <c>OrderCompleted</c> earn as the refund returned
    /// of the price, <c>floor(earn.Points × amountReturned / order.TotalPrice)</c>, capped at what is left
    /// of the earn after the order's earlier refunds. <paramref name="amountReturned"/> is everything the
    /// refund gave back — the card leg plus the credit leg — because the earn was on the whole gross price;
    /// a full refund passes the whole price and so takes everything that is left. It never reads the
    /// currency's divisor, so a divisor edit after completion cannot move it. No-op when the order earned
    /// nothing. → /product/business-rules#money-constants
    /// <para>
    /// Unlike <see cref="RevokeForCancelledOrderAsync"/> (a one-shot full mirror that no-ops on a
    /// second call), this is keyed per refund: each distinct <paramref name="refundKey"/> revokes,
    /// and the SAME key revokes at most once (idempotent — fast-path read on the key plus the filtered
    /// unique-index backstop that collapses a concurrent double-submit). <c>UserId == null</c>
    /// (anonymous/legacy) is a no-op, mirroring the earn and full-revoke skips. Every refund's row carries
    /// <see cref="LoyaltyEarnSource.OrderPartiallyRefunded"/>, the one source the cap sums, so it never
    /// collides with the cancel mirror's <c>(orderId, OrderCancelled)</c> guard.
    /// </para>
    /// <para>
    /// It flushes the unit of work to collapse a duplicate on the key, so a caller makes it the last write
    /// of its command: the flush then lands the command's whole unit, or discards a duplicate's whole unit.
    /// </para>
    /// </summary>
    Task RevokeForRefundAsync(
        string orderId, decimal amountReturned, string refundKey, string actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Compute the tier discount (an amount in the order's currency, not %) for a user + raw subtotal.
    /// Returns (0, null) for anonymous users with no account, (0, tier, floor) when the tier qualifies
    /// but a default-currency order is below the per-tier floor, (discount, tier, floor) otherwise.
    /// <paramref name="currencyId"/> is the ORDER's currency: the floor is a platform-default-currency
    /// number and is applied only when the two agree. Called from <c>OrderFactory</c> and both quotes.
    /// </summary>
    Task<TierDiscountResult> ResolveTierDiscountForOrderAsync(
        string userId, decimal orderTotal, string currencyId, CancellationToken cancellationToken);

    /// <summary>
    /// Grant points outside the order-completion path. The loyalty account is lazily created.
    ///
    /// <para><b>The admin manual path REQUIRES a client-generated request id</b>, persisted as the ledger
    /// row's idempotency key, so a double-submit collapses onto one row via the fast-path lookup and the
    /// filtered unique index behind it. The order-driven and referral paths pass null and stay keyed on
    /// (order, source). → /flows/loyalty-and-memberships</para>
    /// </summary>
    Task GrantPointsManuallyAsync(
        string userId,
        int points,
        LoyaltyEarnSource source,
        string? orderId,
        string actorId,
        string? reason,
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
        string? reason,
        string? requestId,
        CancellationToken cancellationToken);
}
