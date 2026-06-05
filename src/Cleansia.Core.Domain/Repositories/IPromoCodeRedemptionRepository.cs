using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface IPromoCodeRedemptionRepository : IRepository<PromoCodeRedemption, string>
{
    /// <summary>
    /// Count of prior redemptions of a given code by a given user — drives the
    /// per-user-cap check.
    /// </summary>
    Task<int> CountForUserAndCodeAsync(string userId, string promoCodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotency lookup — if a redemption already exists for this order, the
    /// service treats <c>ApplyAsync</c> as a no-op and returns the prior amount.
    /// </summary>
    Task<PromoCodeRedemption?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reserve the next free per-user redemption slot for
    /// <paramref name="userId"/> on <paramref name="promoCodeId"/> AND insert the redemption row in
    /// one statement (S7). The next 0-based <c>SlotOrdinal</c> is computed in SQL
    /// (<c>COALESCE(MAX(SlotOrdinal) + 1, 0)</c>) under a <c>WHERE … &lt; maxRedemptionsPerUser</c>
    /// guard, so the ordinal is DERIVED from the reservation (never a pre-read count). The insert is
    /// guarded by the <c>(TenantId, PromoCodeId, UserId, SlotOrdinal)</c> unique index as a
    /// defense-in-depth backstop with <c>ON CONFLICT DO NOTHING</c> — so a concurrent writer that
    /// computed the same ordinal is rejected silently (no exception), not at a later commit.
    /// <para>
    /// Returns the freshly inserted <see cref="PromoCodeRedemption"/> (carrying its reserved
    /// <c>SlotOrdinal</c> and applied discount) on success, or <c>null</c> when no slot is available
    /// (per-user cap reached, or a race loser) — a RESULT, never an exception. The caller maps
    /// <c>null</c> to <c>PerUserLimitReached</c>.
    /// </para>
    /// <para>
    /// DELIBERATE EXCEPTION to the "never commit outside the UnitOfWork pipeline" rule: like the
    /// global counter, this issues SQL immediately and auto-commits; it is the only direct DB write
    /// in the redeem path and is REQUIRED for atomicity. It does not roll back the order — a
    /// <c>null</c> result just logs (the existing fail-soft contract in CreateOrder).
    /// </para>
    /// </summary>
    Task<PromoCodeRedemption?> TryReserveRedemptionSlotAsync(
        string userId,
        string promoCodeId,
        int maxRedemptionsPerUser,
        string orderId,
        decimal appliedDiscount,
        CancellationToken cancellationToken);

    /// <summary>
    /// Total number of redemptions for a given promo code (admin detail view).
    /// </summary>
    Task<int> CountByPromoCodeAsync(string promoCodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Paged redemption log per code with the redeeming user joined for
    /// email rendering. Ordered most-recent first.
    /// </summary>
    Task<IReadOnlyList<PromoCodeRedemption>> GetPagedByPromoCodeAsync(
        string promoCodeId,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}
