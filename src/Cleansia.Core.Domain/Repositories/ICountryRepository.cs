using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ICountryRepository : IRepository<Country, string>
{
    Task<bool> ExistsWithIsoCodeAsync(string isoCode, CancellationToken cancellationToken);
    Task<Country?> GetByIsoCodeAsync(string isoCode, CancellationToken cancellationToken);
    Task<bool> IsInUseAsync(string countryId, CancellationToken cancellationToken);

    /// <summary>
    /// Countries the company actually operates in (IsServiced && IsActive),
    /// sorted by Name. Customer + partner-facing pickers must use this —
    /// NOT the full catalog.
    /// </summary>
    Task<IReadOnlyList<Country>> GetServicedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True iff <paramref name="countryId"/> resolves to a country flagged
    /// IsServiced && IsActive. Used by address-creation validators.
    /// </summary>
    Task<bool> IsServicedAsync(string countryId, CancellationToken cancellationToken);
}