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
        // Staged on the caller's DbContext rather than self-committed: the Orders row is only
        // change-tracked at this point, so a self-committing INSERT violated the FK on every promo
        // booking. Staging makes EF emit the Orders INSERT first, inside one SaveChanges.
        //
        // THE COST, NAMED: the per-user cap is now an app-level pre-read rather than one atomic
        // statement, so two concurrent same-user redemptions can both compute the same ordinal. Bounded,
        // and strictly better than the outage it replaces. -> /flows/loyalty-and-memberships
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
