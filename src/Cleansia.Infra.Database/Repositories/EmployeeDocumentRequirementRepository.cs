using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeDocumentRequirementRepository(CleansiaDbContext context)
    : BaseRepository<EmployeeDocumentRequirement>(context), IEmployeeDocumentRequirementRepository
{
    public async Task<IReadOnlyList<EmployeeDocumentRequirement>> GetForCountryAsync(
        string countryId,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.CountryId == countryId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.DocumentType)
            .ToListAsync(cancellationToken);
    }
}
