using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.Domain.Repositories;

public interface ICountryConfigurationRepository : IRepository<CountryConfiguration, string>
{
    Task<CountryConfiguration?> GetByCountryIdAsync(string countryId, CancellationToken cancellationToken);
    Task<bool> ExistsForCountryAsync(string countryId, CancellationToken cancellationToken);
}
