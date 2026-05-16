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

    public Task<int> CountByPromoCodeAsync(string promoCodeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .CountAsync(r => r.PromoCodeId == promoCodeId, cancellationToken);
    }

    public async Task<IReadOnlyList<PromoCodeRedemption>> GetPagedByPromoCodeAsync(
        string promoCodeId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .AsNoTracking()
            .Where(r => r.PromoCodeId == promoCodeId)
            .OrderByDescending(r => r.RedeemedOn)
            .Skip(offset)
            .Take(limit)
            .Include(r => r.User)
            .ToListAsync(cancellationToken);
    }
}
