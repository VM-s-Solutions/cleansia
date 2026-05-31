using Cleansia.Core.Domain.ServiceAreas;

namespace Cleansia.Core.Domain.Repositories;

public interface IServiceCityRepository : IRepository<ServiceCity, string>
{
    Task<IReadOnlyList<ServiceCity>> GetByCountryAsync(string countryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceCity>> GetAllActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True iff a ServiceCity exists in <paramref name="countryId"/> whose
    /// <see cref="ServiceCity.Name"/> matches <paramref name="cityName"/>
    /// case-insensitively. Whitespace is trimmed off both sides.
    /// </summary>
    Task<bool> CityIsServicedAsync(string countryId, string cityName, CancellationToken cancellationToken);

    Task<bool> ExistsWithNameInCountryAsync(string countryId, string name, string? excludeId, CancellationToken cancellationToken);
}
