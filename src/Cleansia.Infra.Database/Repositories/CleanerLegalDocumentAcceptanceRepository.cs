using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CleanerLegalDocumentAcceptanceRepository(CleansiaDbContext context)
    : BaseRepository<CleanerLegalDocumentAcceptance>(context), ICleanerLegalDocumentAcceptanceRepository
{
    public async Task<IReadOnlyList<CleanerLegalDocumentAcceptance>> GetByEmployeeIdNoTrackingAsync(
        string employeeId, CancellationToken cancellationToken)
    {
        return await GetQueryableIgnoringTenant()
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.AcceptedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> PseudonymiseForEmployeeAsync(string employeeId, CancellationToken cancellationToken)
    {
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
}
