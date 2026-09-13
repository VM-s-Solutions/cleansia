using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.Domain.Repositories;

public interface ICountryConfigurationRepository : IRepository<CountryConfiguration, string>
{
    Task<CountryConfiguration?> GetByCountryIdAsync(string countryId, CancellationToken cancellationToken);
    Task<bool> ExistsForCountryAsync(string countryId, CancellationToken cancellationToken);

    /// <summary>The configuration flagged as the default market, with its country; null when none is.</summary>
    Task<CountryConfiguration?> GetDefaultMarketAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears the flag on every row that carries it -- ALL rather than "the" one, for the reason
    /// <see cref="ICurrencyRepository.ClearDefaultAsync"/> gives: a clear keyed on a prior read races
    /// a snapshot and cannot recover a database that has none.
    /// </summary>
    Task ClearDefaultMarketAsync(CancellationToken cancellationToken);
}
