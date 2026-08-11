using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class PromoCodeRedemptionRepository(CleansiaDbContext context)
    : BaseRepository<PromoCodeRedemption>(context), IPromoCodeRedemptionRepository
{
    public Task<int> CountForUserAndCodeAsync(string userId, string promoCodeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .CountAsync(r => r.UserId == userId && r.PromoCodeId == promoCodeId, cancellationToken);
    }

    public Task<PromoCodeRedemption?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
    }

    // INTERIM(ADR-0038 §D3 → T-0532): change-tracked insert; delete when IPostCommitEffects lands.
    public async Task<PromoCodeRedemption?> TryReserveRedemptionSlotAsync(
        string userId,
        string promoCodeId,
        int maxRedemptionsPerUser,
        string orderId,
        decimal appliedDiscount,
        CancellationToken cancellationToken)
    {
        // The Orders row this points at is only CHANGE-TRACKED when we get here — CreateOrder's
        // UnitOfWork commit runs after the handler returns — so the self-committing INSERT this
        // replaces violated FK_PromoCodeRedemptions_Orders_OrderId (23503) on every promo booking.
        // Staging the row on the same DbContext makes EF emit the Orders INSERT before this
        // dependent one inside the single SaveChangesAsync.
        //
        // The cost, named: the per-user cap is now an app-level pre-read, not a single atomic
        // statement. Two concurrent same-user redemptions can both compute the same ordinal; with a
        // NULL tenant the unique index is nulls-distinct and both land, and with a non-null tenant
        // the index fires as a DbUpdateException at the order's commit rather than as a clean null.
        // Both are bounded and strictly better than the outage. The end state restores the atomic
        // statement byte-for-byte and runs it after the commit — see T-0532.
        var highestOrdinal = await GetDbSet()
            .Where(r => r.PromoCodeId == promoCodeId && r.UserId == userId)
            .Select(r => (int?)r.SlotOrdinal)
            .MaxAsync(cancellationToken);

        var nextOrdinal = (highestOrdinal ?? -1) + 1;
        if (nextOrdinal >= maxRedemptionsPerUser)
        {
            return null;
        }

        var redemption = PromoCodeRedemption.CreateReserved(
            promoCodeId, userId, orderId, appliedDiscount, nextOrdinal);
        Add(redemption);
        return redemption;
    }

    public Task<int> CountByPromoCodeAsync(string promoCodeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .CountAsync(r => r.PromoCodeId == promoCodeId, cancellationToken);
    }
}
