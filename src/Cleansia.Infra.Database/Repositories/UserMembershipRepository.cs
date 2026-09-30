using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserMembershipRepository(CleansiaDbContext context)
    : BaseRepository<UserMembership>(context), IUserMembershipRepository
{
    public Task<UserMembership?> GetLatestPaidForUserAsync(string userId, CancellationToken cancellationToken) =>
        GetDbSet().Where(m => m.UserId == userId && (m.PaidPeriodConfirmedAt != null || m.TrialEndsAtUtc != null))
            .OrderByDescending(m => m.PaidPeriodConfirmedAt ?? m.TrialEndsAtUtc)
            .ThenByDescending(m => m.CreatedOn).ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserMembership?> GetLifecycleForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return LifecycleForUserQuery(userId).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserMembership?> GetLifecycleForUserNoTrackingAsync(string userId, CancellationToken cancellationToken)
    {
        return LifecycleForUserQuery(userId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserMembership?> GetEntitledForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return EntitledForUserQuery(userId).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserMembership?> GetEntitledForUserNoTrackingAsync(string userId, CancellationToken cancellationToken)
    {
        return EntitledForUserQuery(userId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<UserMembership> EntitledForUserQuery(string userId)
    {
        return LifecycleForUserQuery(userId)
            // Benefits follow the account even while an authorized order is operated elsewhere.
            .IgnoreQueryFilters()
            .Where(m => m.Status == MembershipStatus.Active);
    }

    private IQueryable<UserMembership> LifecycleForUserQuery(string userId)
    {
        return GetDbSet()
            .Include(m => m.MembershipPlan)
            .Include(m => m.Currency)
            .Where(m => m.UserId == userId
                && (m.Status == MembershipStatus.Active
                    || m.Status == MembershipStatus.PastDue
                    || m.Status == MembershipStatus.Paused)
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

    public Task<bool> HasAnyInOtherCurrencyAsync(string userId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(m => m.UserId == userId && m.CurrencyId != currencyId, cancellationToken);
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
        // Monthly first so it is the default selection on the switcher; the code is the tiebreak, since
        // a plan's price is per currency and no single one can order the list.
        return await GetDbSet()
            .Where(p => p.IsActive)
            .OrderBy(p => p.BillingInterval)
            .ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);
    }
}
