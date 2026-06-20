using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserMembershipRepository(CleansiaDbContext context)
    : BaseRepository<UserMembership>(context), IUserMembershipRepository
{
    public Task<UserMembership?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return ActiveForUserQuery(userId).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserMembership?> GetActiveForUserNoTrackingAsync(string userId, CancellationToken cancellationToken)
    {
        return ActiveForUserQuery(userId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<UserMembership> ActiveForUserQuery(string userId)
    {
        return GetDbSet()
            .Include(m => m.MembershipPlan)
            // IsActive on the entity is a computed property combining Status
            // AND CurrentPeriodEnd > now. Filter both server-side so we don't
            // pull cancelled rows back into memory just to drop them.
            .Where(m => m.UserId == userId
                && m.Status == MembershipStatus.Active
                && m.CurrentPeriodEnd > DateTime.UtcNow)
            .OrderByDescending(m => m.CurrentPeriodEnd);
    }

    public Task<UserMembership?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
    {
        // Cross-tenant by design: webhook lookup. Caller (HandleSubscriptionEvent)
        // sets ITenantProvider.SetTenantOverride(membership.TenantId) before any
        // mutation so child rows inherit the right tenant.
        return GetDbSet()
            .IgnoreQueryFilters()
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

    public async Task<(IReadOnlyList<MembershipPlan> Items, int Total)> GetPagedAdminAsync(
        bool? active,
        string? search,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = GetDbSet().AsNoTracking();

        if (active.HasValue)
        {
            query = active.Value
                ? query.Where(p => p.IsActive)
                : query.Where(p => !p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim().ToUpperInvariant();
            query = query.Where(p =>
                EF.Functions.Like(p.Code, $"%{needle}%")
                || EF.Functions.Like(p.Name.ToUpper(), $"%{needle}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.BillingInterval)
            .ThenBy(p => p.MonthlyPriceCzk)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
