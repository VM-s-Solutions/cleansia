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

    /// <summary>
    /// The comparison runs IN MEMORY over one country's active rows, not in SQL.
    ///
    /// <para>It has to: the fold strips diacritics, and <c>unaccent</c> is not a registered extension on
    /// this context (<c>CleansiaDbContext</c> registers <c>citext</c> and <c>pg_trgm</c> only), so
    /// Postgres cannot express the predicate. The cost is bounded by the row count for a single
    /// country — ten for Czechia today, and this is one lookup per order creation, not per row of a
    /// list.</para>
    ///
    /// <para>Only the NAMES are materialised, so the projection stays one column.</para>
    /// </summary>
    public async Task<bool> CityIsServicedAsync(string countryId, string cityName, CancellationToken cancellationToken)
    {
        var servicedNames = await GetDbSet()
            .Where(c => c.CountryId == countryId && c.IsActive)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        return servicedNames.Any(name => CityNameMatch.Matches(name, cityName));
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
