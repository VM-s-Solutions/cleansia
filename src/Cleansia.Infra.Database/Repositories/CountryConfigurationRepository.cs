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
}
