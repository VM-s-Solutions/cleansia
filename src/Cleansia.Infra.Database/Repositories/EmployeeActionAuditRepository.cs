using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeActionAuditRepository(CleansiaDbContext context, IServiceScopeFactory serviceScopeFactory)
    : BaseRepository<EmployeeActionAudit>(context), IEmployeeActionAuditRepository
{
    private const int DeleteBatchSize = 100;

    public async Task RecordOnceOutOfBandAsync(EmployeeActionAudit entry, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var ownContext = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        await using var transaction = await ownContext.Database.BeginTransactionAsync(cancellationToken);

        // The caller supplies the authorized order and its company. Lock that existing row before the
        // absence check, so concurrent first reads cannot both insert a row with different audit ids.
        await ownContext.Database.ExecuteSqlAsync($"""
            SELECT "Id" FROM "Orders"
            WHERE "Id" = {entry.OrderId} AND "TenantId" = {entry.TenantId}
            FOR UPDATE
            """, cancellationToken);

        var alreadyRecorded = await ownContext.EmployeeActionAudits.IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == entry.TenantId
                && a.EmployeeId == entry.EmployeeId
                && a.OrderId == entry.OrderId
                && a.Action == entry.Action, cancellationToken);
        if (alreadyRecorded)
        {
            return;
        }

        ownContext.EmployeeActionAudits.Add(entry);
        await ownContext.CommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0;

        while (true)
        {
            var batch = await GetQueryable()
                .Where(a => a.CreatedOn < cutoff)
                .OrderBy(a => a.CreatedOn)
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
