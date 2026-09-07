using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.Domain.Repositories;

public interface IPropertySizePresetRepository : IRepository<PropertySizePreset, string>
{
    /// <summary>
    /// The active presets for one market, in display order.
    /// </summary>
    /// <remarks>
    /// Catalogue data, so there is no tenant scoping to apply — the entity is
    /// deliberately not <c>ITenantEntity</c>. → /decisions/adr-0056
    /// </remarks>
    Task<IReadOnlyList<PropertySizePreset>> GetForCountryAsync(
        string countryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same list resolved by ISO code, for callers that hold a country code
    /// rather than an id — the public site knows "CZE", not a ULID.
    /// </summary>
    Task<IReadOnlyList<PropertySizePreset>> GetForCountryIsoCodeAsync(
        string isoCode,
        CancellationToken cancellationToken);
}
