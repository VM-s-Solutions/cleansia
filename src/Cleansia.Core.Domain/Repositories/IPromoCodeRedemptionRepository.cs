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
    /// Reserve the next free 0-based per-user redemption slot for <paramref name="userId"/> on
    /// <paramref name="promoCodeId"/> and stage the redemption row for the caller's unit of work.
    /// Returns the row (carrying its reserved <c>SlotOrdinal</c> and applied discount) on success,
    /// or <c>null</c> when no slot is available (per-user cap reached) — a RESULT, never an
    /// exception. The caller maps <c>null</c> to <c>PerUserLimitReached</c>.
    /// <para>
    /// INTERIM (ADR-0038 §D3 → T-0532): the row is CHANGE-TRACKED and lands on the caller's commit,
    /// because the <c>Orders</c> row it references is itself only tracked at this point. That trades
    /// the previous single-statement atomic cap for an app-level pre-read of the highest ordinal —
    /// see the implementation for the two concurrency residuals. The end state moves the reservation
    /// strictly after the commit and restores the atomic statement unchanged.
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
}
