using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class WorkContractAcceptanceRepository(CleansiaDbContext context)
    : BaseRepository<WorkContractAcceptance>(context), IWorkContractAcceptanceRepository
{
    public Task<WorkContractAcceptance?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
    {
        return GetQueryableIgnoringTenant()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkContractAcceptanceRow>> GetForSeatsAsync(
        IReadOnlyCollection<string> orderEmployeeIds, CancellationToken cancellationToken)
    {
        if (orderEmployeeIds.Count == 0)
        {
            return [];
        }

        return await Rows(GetQueryableIgnoringTenant().Where(a => orderEmployeeIds.Contains(a.OrderEmployeeId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkContractAcceptanceRow>> GetForOrdersAsync(
        IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        return await Rows(GetQueryableIgnoringTenant().Where(a => orderIds.Contains(a.OrderId)))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyForSeatAsync(string orderEmployeeId, CancellationToken cancellationToken)
    {
        return GetQueryable().AnyAsync(a => a.OrderEmployeeId == orderEmployeeId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkContractAcceptanceRow>> GetByEmployeeIdNoTrackingAsync(
        string employeeId, CancellationToken cancellationToken)
    {
        return await Rows(GetQueryableIgnoringTenant().Where(a => a.EmployeeId == employeeId), newestFirst: true)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> PseudonymiseForEmployeeAsync(string employeeId, CancellationToken cancellationToken)
    {
        // Tracked, not ExecuteUpdateAsync: the blanking must ride the erasure's single commit, so a
        // commit failure cannot leave the trail blanked while the subject still exists (or the reverse).
        var rows = await GetQueryableIgnoringTenant()
            .Where(a => a.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Pseudonymise();
        }

        return rows.Count;
    }

    public async Task<int> PseudonymiseExpiredAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var total = 0;

        while (true)
        {
            var batch = await GetQueryable()
                .Where(a => a.AcceptedOn < cutoff
                            && (a.IpAddress != null || a.DeviceLabel != null || a.DeviceId != null))
                .OrderBy(a => a.AcceptedOn)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var row in batch)
            {
                row.Pseudonymise();
            }

            await CommitAsync(cancellationToken);
            total += batch.Count;
        }

        return total;
    }

    // The two joins every reader wants: the language lives on the text row the acceptance names, the
    // display number on the order. Both principals are platform-wide or already the caller's, so the
    // filter bypass on the order is only what lets a cross-border customer's row resolve its number.
    // Ordered before the final projection: an ORDER BY through a record constructor does not translate.
    private IQueryable<WorkContractAcceptanceRow> Rows(IQueryable<WorkContractAcceptance> acceptances, bool newestFirst = false)
    {
        var joined =
            from a in acceptances.AsNoTracking()
            join t in Context.LegalDocumentTexts.AsNoTracking() on a.LegalDocumentTextId equals t.Id
            join o in Context.Orders.IgnoreQueryFilters().AsNoTracking() on a.OrderId equals o.Id
            select new { Acceptance = a, t.Language, o.DisplayOrderNumber };

        var ordered = newestFirst
            ? joined.OrderByDescending(x => x.Acceptance.AcceptedOn)
            : joined.OrderBy(x => x.Acceptance.AcceptedOn);

        return ordered.Select(x => new WorkContractAcceptanceRow(
            x.Acceptance.Id,
            x.Acceptance.OrderId,
            x.DisplayOrderNumber,
            x.Acceptance.OrderEmployeeId,
            x.Acceptance.EmployeeId,
            x.Acceptance.LegalDocumentTextId,
            x.Acceptance.DocumentVersion,
            x.Language,
            x.Acceptance.AcceptedOn,
            x.Acceptance.ClientAudience,
            x.Acceptance.IpAddress,
            x.Acceptance.DeviceLabel,
            x.Acceptance.DeviceId,
            x.Acceptance.FactsJson));
    }
}
