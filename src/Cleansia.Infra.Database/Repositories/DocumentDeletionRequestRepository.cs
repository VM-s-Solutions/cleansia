using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class DocumentDeletionRequestRepository(CleansiaDbContext context)
    : BaseRepository<DocumentDeletionRequest>(context), IDocumentDeletionRequestRepository
{
    public Task<DocumentDeletionRequest?> GetOpenForDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(
                r => r.DocumentId == documentId
                    && r.Status == DocumentDeletionRequestStatus.Pending,
                cancellationToken);
    }

    public async Task RemoveForEmployeeAsync(string employeeId, CancellationToken cancellationToken)
    {
        var rows = await GetQueryableIgnoringTenant()
            .Where(r => r.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        GetDbSet().RemoveRange(rows);
    }
}
