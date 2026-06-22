using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ReferralRepository(CleansiaDbContext context)
    : BaseRepository<Referral>(context), IReferralRepository
{
    public Task<Referral?> GetByReferredUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<Referral?>(null);
        }

        return GetDbSet()
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<ReferralStatus, int>> GetStatusCountsByReferrerAsync(
        string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new Dictionary<ReferralStatus, int>();
        }

        var grouped = await GetDbSet()
            .Where(r => r.ReferrerUserId == userId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task<IReadOnlyList<Referral>> GetExpirableAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.Status == ReferralStatus.Accepted && r.AcceptedOn < cutoff)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Referral> Items, int Total)> GetPagedAdminAsync(
        ReferralStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = GetDbSet().AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(r => r.AcceptedOn >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(r => r.AcceptedOn <= dateTo.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.AcceptedOn)
            .Skip(offset)
            .Take(limit)
            .Include(r => r.Referrer)
            .Include(r => r.Referred)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<Referral>> GetByUserAsync(
        string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<Referral>();
        }

        return await GetDbSet()
            .AsNoTracking()
            .Where(r => r.ReferrerUserId == userId || r.ReferredUserId == userId)
            .OrderByDescending(r => r.AcceptedOn)
            .Include(r => r.Referrer)
            .Include(r => r.Referred)
            .ToListAsync(cancellationToken);
    }
}
