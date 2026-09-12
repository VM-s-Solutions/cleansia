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

    public Task<UserMembership?> GetEntitledForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return EntitledForUserQuery(userId).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserMembership?> GetEntitledForUserNoTrackingAsync(string userId, CancellationToken cancellationToken)
    {
        return EntitledForUserQuery(userId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    // Entitlement = a live enrolment that is also PAID. The trial conjunct is spelled out rather than
    // calling UserMembership.IsInTrialAt, which is a computed property and would not translate — EF would
    // either throw or, worse, evaluate it client-side after pulling the row.
    private IQueryable<UserMembership> EntitledForUserQuery(string userId)
    {
        var now = DateTime.UtcNow;
        return ActiveForUserQuery(userId)
            .Where(m => m.TrialEndsAtUtc == null || m.TrialEndsAtUtc <= now);
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

    public Task<bool> HasEverStartedTrialAsync(string userId, CancellationToken cancellationToken)
    {
        // Every status, including soft-deleted rows: the question is historical. Seeks the
        // (UserId, Status) index on its leading column.
        return GetDbSet()
            .AnyAsync(m => m.UserId == userId && m.TrialEndsAtUtc != null, cancellationToken);
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
}
