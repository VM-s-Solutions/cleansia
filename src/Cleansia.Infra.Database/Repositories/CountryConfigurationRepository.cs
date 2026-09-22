using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CountryConfigurationRepository(CleansiaDbContext context) : BaseRepository<CountryConfiguration>(context), ICountryConfigurationRepository
{
    public Task<CountryConfiguration?> GetByCountryIdAsync(string countryId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Country)
            .FirstOrDefaultAsync(c => c.CountryId == countryId, cancellationToken);
    }

    public Task<bool> ExistsForCountryAsync(string countryId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(c => c.CountryId == countryId, cancellationToken);
    }

    public Task<CountryConfiguration?> GetDefaultMarketAsync(CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(c => c.Country)
            .FirstOrDefaultAsync(c => c.IsDefaultMarket, cancellationToken);
    }

    public async Task<IReadOnlyList<CountryConfiguration>> GetOperatedByAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .AsNoTracking()
            .Where(c => c.OperatorTenantId == tenantId)
            .OrderBy(c => c.CountryId)
            .ToListAsync(cancellationToken);
    }

    public async Task ClearDefaultMarketAsync(CancellationToken cancellationToken)
    {
        var flagged = await GetDbSet()
            .Where(c => c.IsDefaultMarket)
            .ToListAsync(cancellationToken);

        foreach (var configuration in flagged)
        {
            configuration.SetAsDefaultMarket(false);
        }
    }
}
