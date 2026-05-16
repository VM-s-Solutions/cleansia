using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeDocumentRepository(CleansiaDbContext context) : BaseRepository<EmployeeDocument>(context), IEmployeeDocumentRepository
{
    public Task<EmployeeDocument?> GetByIdWithVersionHistoryAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(d => d.PreviousVersion)
            .Include(d => d.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public Task<List<EmployeeDocument>> GetByEmployeeIdAsync(string employeeId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = GetDbSet()
            .Include(d => d.Employee)
            .Where(d => d.EmployeeId == employeeId);

        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return query
            .OrderByDescending(d => d.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EmployeeDocument>> GetVersionHistoryAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(documentId, cancellationToken);
        if (document == null)
            return [];

        var versions = new List<EmployeeDocument> { document };
        var currentDoc = document;

        while (currentDoc.PreviousVersionId != null)
        {
            currentDoc = await GetDbSet()
                .Include(d => d.PreviousVersion)
                .FirstOrDefaultAsync(d => d.Id == currentDoc.PreviousVersionId, cancellationToken);

            if (currentDoc != null)
            {
                versions.Add(currentDoc);
            }
            else
            {
                break;
            }
        }

        return versions.OrderBy(v => v.Version).ToList();
    }

    public Task<EmployeeDocument?> GetLatestVersionAsync(string documentId, CancellationToken cancellationToken = default)
    {
        // Find all documents in the version chain and get the one with highest version
        return GetDbSet()
            .Where(d => d.Id == documentId || d.PreviousVersionId == documentId)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<EmployeeDocument?> GetLatestByFileNameAsync(string employeeId, string fileName, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Where(d => d.EmployeeId == employeeId && d.FileName == fileName && d.IsActive)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public override Task<EmployeeDocument?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(d => d.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
}
