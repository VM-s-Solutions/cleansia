using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface IReferralRepository : IRepository<Referral, string>
{
    /// <summary>
    /// "Has this user already accepted a referral?" — returns the row in any
    /// status (Accepted / Qualified / Expired) so the service can enforce the
    /// one-referral-per-invitee rule.
    /// </summary>
    Task<Referral?> GetByReferredUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Paged "people I invited" list for the inviter's referrals tab.
    /// Includes the invitee user for name resolution.
    /// </summary>
    Task<IReadOnlyList<Referral>> GetByReferrerAsync(
        string userId, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Total count of referrals where the given user is the referrer (for
    /// paged-list metadata and the Rewards-tab summary stat).
    /// </summary>
    Task<int> CountByReferrerAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Per-status counts of referrals where the given user is the referrer, computed with a single
    /// grouped query over the indexed ReferrerUserId — replaces materialising every row (with the
    /// invitee included) just to count statuses in memory. Statuses with no rows are absent from the map.
    /// </summary>
    Task<IReadOnlyDictionary<ReferralStatus, int>> GetStatusCountsByReferrerAsync(
        string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Background expiry sweep: returns Accepted referrals whose AcceptedOn
    /// is older than the cutoff. Caller flips them to Expired.
    /// </summary>
    Task<IReadOnlyList<Referral>> GetExpirableAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    /// Admin-side paged list across all users with optional filters on
    /// <paramref name="status"/> and the AcceptedOn date range.
    /// Includes Referrer + Referred users for email rendering. Returns the
    /// materialised page plus the unfiltered total.
    /// </summary>
    Task<(IReadOnlyList<Referral> Items, int Total)> GetPagedAdminAsync(
        ReferralStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int offset,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// All referrals where the given user is EITHER the referrer or the
    /// referred party. Includes both users on each row for email rendering.
    /// No pagination — a user typically has &lt; 100 referrals total.
    /// </summary>
    Task<IReadOnlyList<Referral>> GetByUserAsync(string userId, CancellationToken cancellationToken);
}
