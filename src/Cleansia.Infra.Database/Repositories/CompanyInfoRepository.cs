using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CompanyInfoRepository(CleansiaDbContext context) : BaseRepository<CompanyInfo>(context), ICompanyInfoRepository
{
    public async Task<CompanyInfo?> GetActiveCompanyInfoAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet().FirstOrDefaultAsync(c => c.IsActive, cancellationToken);
    }
}
