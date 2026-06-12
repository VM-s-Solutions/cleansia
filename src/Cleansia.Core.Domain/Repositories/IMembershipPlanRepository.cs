using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Catalog of subscription plans. Initially seeded with one row ("Cleansia
/// Plus") at product launch via SQL insert; admins manage future plans
/// through a back-office surface that doesn't exist yet.
/// </summary>
public interface IMembershipPlanRepository : IRepository<MembershipPlan, string>
{
    /// <summary>Lookup by stable code (e.g. <c>PLUS_MONTHLY</c>). Returns null if not found.</summary>
    Task<MembershipPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// All <see cref="MembershipPlan.IsActive"/> = true plans, ordered by
    /// billing interval (Monthly first, Yearly second). Drives the customer
    /// plan switcher on the subscribe screen.
    /// </summary>
    Task<IReadOnlyList<MembershipPlan>> GetActivePlansAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Admin back-office paged list across ALL plans (active and inactive),
    /// with an optional active filter and a case-insensitive code/name search.
    /// Returns the materialised page plus the unfiltered (by paging) total.
    /// </summary>
    Task<(IReadOnlyList<MembershipPlan> Items, int Total)> GetPagedAdminAsync(
        bool? active,
        string? search,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}
