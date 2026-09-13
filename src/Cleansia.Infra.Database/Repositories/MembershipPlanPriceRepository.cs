using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class MembershipPlanPriceRepository(CleansiaDbContext context)
    : BaseRepository<MembershipPlanPrice>(context), IMembershipPlanPriceRepository
{
    public async Task<Dictionary<string, MembershipPlanPrice>> GetForPlansAsync(
        IReadOnlyCollection<string> planIds, string currencyId, CancellationToken cancellationToken)
    {
        if (planIds.Count == 0)
        {
            return [];
        }

        return await GetDbSet()
            .AsNoTracking()
            .Where(p => p.CurrencyId == currencyId && planIds.Contains(p.MembershipPlanId))
            .ToDictionaryAsync(p => p.MembershipPlanId, cancellationToken);
    }

    public Task<MembershipPlanPrice?> GetForPlanAsync(string planId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(p => p.MembershipPlanId == planId && p.CurrencyId == currencyId, cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipPlanPrice>> GetAllForPlanAsync(string planId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(p => p.Currency)
            .AsNoTracking()
            .Where(p => p.MembershipPlanId == planId)
            .OrderBy(p => p.Currency!.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsStripePriceIdUsedAsync(string stripePriceId, string? exceptPlanId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(p => p.StripePriceId == stripePriceId && (exceptPlanId == null || p.MembershipPlanId != exceptPlanId), cancellationToken);
    }
}
