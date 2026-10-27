using Cleansia.Core.Domain.ServiceAreas;

namespace Cleansia.Core.Domain.Repositories;

public interface IServiceCityRepository : IRepository<ServiceCity, string>
{
    Task<IReadOnlyList<ServiceCity>> GetByCountryAsync(string countryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceCity>> GetAllActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True iff an active ServiceCity in <paramref name="countryId"/> serves the address by
    /// <see cref="CityNameMatch.Serves"/>: by its postcode on a row with a ZIP prefix, by the city name otherwise.
    /// </summary>
    Task<bool> CityIsServicedAsync(string countryId, string cityName, string? zipCode, CancellationToken cancellationToken);

    Task<bool> ExistsWithNameInCountryAsync(string countryId, string name, string? excludeId, CancellationToken cancellationToken);
}
