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
