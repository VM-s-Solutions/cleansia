using System.Linq.Expressions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CustomerActionAuditRepository(CleansiaDbContext context)
    : BaseRepository<CustomerActionAudit>(context), ICustomerActionAuditRepository
{
    private const int DeleteBatchSize = 100;

    public IQueryable<CustomerActionAudit> GetQueryableForUser(string userId)
    {
        return GetQueryableIgnoringTenant().Where(a => a.UserId == userId);
    }

    public Task<int> GetCountForUserAsync(string userId, Expression<Func<CustomerActionAudit, bool>>? filter, CancellationToken cancellationToken)
    {
        var query = GetQueryableForUser(userId);
        return (filter is null ? query : query.Where(filter)).CountAsync(cancellationToken);
    }

    public IQueryable<CustomerActionAudit> GetPagedSortForUser<TSort>(
        string userId, int offset, int limit, Expression<Func<CustomerActionAudit, bool>>? filter, IEnumerable<SortDefinition> sort)
        where TSort : BaseSort<CustomerActionAudit>
        => PagedSort<TSort>(GetQueryableForUser(userId), offset, limit, filter, sort);

    public async Task<int> PseudonymiseForSubjectAsync(string userId, CancellationToken cancellationToken)
    {
        // Tracked, not ExecuteUpdateAsync: the blanking must ride the erasure's single commit, so a
        // commit failure cannot leave the trail blanked while the subject still exists (or the reverse).
        var rows = await GetQueryableIgnoringTenant()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Pseudonymise();
        }

        return rows.Count;
    }

    public async Task<int> PseudonymiseGuestRowsForOrdersAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return 0;
        }

        var rows = await GetQueryableIgnoringTenant()
            .Where(a => a.UserId == null && a.ResourceType == nameof(Order) && orderIds.Contains(a.ResourceId!))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Pseudonymise();
        }

        return rows.Count;
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0;

        while (true)
        {
            var batch = await GetQueryable()
                .Where(a => a.OccurredOn < cutoff)
                .OrderBy(a => a.OccurredOn)
                .Select(a => a.Id)
                .Take(DeleteBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            total += await GetQueryable()
                .Where(a => batch.Contains(a.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        return total;
    }
}
