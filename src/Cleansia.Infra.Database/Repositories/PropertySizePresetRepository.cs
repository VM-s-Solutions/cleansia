using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class PropertySizePresetRepository(CleansiaDbContext context)
    : BaseRepository<PropertySizePreset>(context), IPropertySizePresetRepository
{
    public async Task<IReadOnlyList<PropertySizePreset>> GetForCountryAsync(
        string countryId,
        CancellationToken cancellationToken)
    {
        // Served by IX_PropertySizePresets_CountryId_SortOrder.
        return await GetDbSet()
            .Where(p => p.CountryId == countryId && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PropertySizePreset>> GetForCountryIsoCodeAsync(
        string isoCode,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(p => p.IsActive && p.Country != null && p.Country.IsoCode == isoCode)
            .OrderBy(p => p.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
