using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserMembershipRepository(CleansiaDbContext context)
    : BaseRepository<UserMembership>(context), IUserMembershipRepository
{
    public Task<UserMembership?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(m => m.MembershipPlan)
            // IsActive on the entity is a computed property combining Status
            // AND CurrentPeriodEnd > now. Filter both server-side so we don't
            // pull cancelled rows back into memory just to drop them.
            .Where(m => m.UserId == userId
                && m.Status == MembershipStatus.Active
                && m.CurrentPeriodEnd > DateTime.UtcNow)
            .OrderByDescending(m => m.CurrentPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserMembership?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.StripeSubscriptionId == stripeSubscriptionId, cancellationToken);
    }
}

public class MembershipPlanRepository(CleansiaDbContext context)
    : BaseRepository<MembershipPlan>(context), IMembershipPlanRepository
{
    public Task<MembershipPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.ToUpperInvariant();
        return GetDbSet()
            .FirstOrDefaultAsync(p => p.Code == normalized && p.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipPlan>> GetActivePlansAsync(CancellationToken cancellationToken)
    {
        // Order: Monthly first so it's the default selection on the switcher;
        // then by price ascending as a tiebreaker (handy when more plans land).
        return await GetDbSet()
            .Where(p => p.IsActive)
            .OrderBy(p => p.BillingInterval)
            .ThenBy(p => p.MonthlyPriceCzk)
            .ToListAsync(cancellationToken);
    }
}
