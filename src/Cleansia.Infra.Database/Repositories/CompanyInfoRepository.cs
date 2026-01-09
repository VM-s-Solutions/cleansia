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

    public async Task<CompanyInfo?> GetActiveByCountryAsync(string countryId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(c => c.Country)
            .FirstOrDefaultAsync(c => c.CountryId == countryId && c.IsActive, cancellationToken);
    }

    public async Task<bool> ExistsActiveForCountryAsync(string countryId, CancellationToken cancellationToken)
    {
        return await GetDbSet().AnyAsync(c => c.CountryId == countryId && c.IsActive, cancellationToken);
    }

    public async Task<bool> ExistsActiveForCountryExcludingAsync(string countryId, string excludeId, CancellationToken cancellationToken)
    {
        return await GetDbSet().AnyAsync(c => c.CountryId == countryId && c.IsActive && c.Id != excludeId, cancellationToken);
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet().CountAsync(c => c.IsActive, cancellationToken);
    }
}
