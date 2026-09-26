using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class AdminActionAuditRepository(CleansiaDbContext context)
    : BaseRepository<AdminActionAudit>(context), IAdminActionAuditRepository
{
    private const int DeleteBatchSize = 100;

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
