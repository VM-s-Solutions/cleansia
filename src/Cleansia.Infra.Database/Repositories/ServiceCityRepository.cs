using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ServiceCityRepository(CleansiaDbContext context)
    : BaseRepository<ServiceCity>(context), IServiceCityRepository
{
    public async Task<IReadOnlyList<ServiceCity>> GetByCountryAsync(string countryId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(c => c.CountryId == countryId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceCity>> GetAllActiveAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(c => c.IsActive)
            .Include(c => c.Country)
            .OrderBy(c => c.Country.Name).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CityIsServicedAsync(string countryId, string cityName, CancellationToken cancellationToken)
    {
        var normalized = cityName.Trim().ToLower();
        return GetDbSet()
            .Where(c => c.CountryId == countryId && c.IsActive)
            .AnyAsync(c => c.Name.ToLower() == normalized, cancellationToken);
    }

    public Task<bool> ExistsWithNameInCountryAsync(string countryId, string name, string? excludeId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();
        return GetDbSet()
            .Where(c => c.CountryId == countryId && c.Name.ToLower() == normalized)
            .Where(c => excludeId == null || c.Id != excludeId)
            .AnyAsync(cancellationToken);
    }
}
