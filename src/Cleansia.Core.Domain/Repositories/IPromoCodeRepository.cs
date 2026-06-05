using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface IPromoCodeRepository : IRepository<PromoCode, string>
{
    /// <summary>
    /// Lookup a code by its (canonical, uppercase) <see cref="PromoCode.Code"/>
    /// value. Tenant scoping is handled by the EF global query filter.
    /// </summary>
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically bump the denormalised global redemption counter, guarded by the global cap, in a
    /// single conditional SQL UPDATE (T-0110 / S7):
    /// <c>UPDATE PromoCodes SET CurrentRedemptionsCount = CurrentRedemptionsCount + 1
    /// WHERE Id = @id AND (GlobalMaxRedemptions IS NULL OR CurrentRedemptionsCount &lt; GlobalMaxRedemptions)</c>.
    /// Returns <c>true</c> when a row was updated (the slot was reserved), <c>false</c> when 0 rows
    /// were affected (the global cap is already reached). This closes the read-then-increment race
    /// on the global cap — the database, not an app-layer read, is the arbiter.
    /// <para>
    /// DELIBERATE EXCEPTION to the "never commit outside the UnitOfWork pipeline" rule: this issues
    /// SQL immediately and auto-commits (it is not change-tracked). That is intentional and REQUIRED
    /// for atomicity; it does not roll back the order — it either reserves the global slot or reports
    /// the cap is reached.
    /// </para>
    /// </summary>
    Task<bool> TryIncrementGlobalRedemptionsAsync(string promoCodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Admin-side paged query — accepts optional flags for active/expired
    /// status and a code search prefix. Returns the materialised page plus
    /// the unfiltered total. Tenant scoping is handled by the EF global
    /// query filter.
    /// </summary>
    Task<(IReadOnlyList<PromoCode> Items, int Total)> GetPagedAdminAsync(
        bool? active,
        bool? expired,
        string? searchCode,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}
