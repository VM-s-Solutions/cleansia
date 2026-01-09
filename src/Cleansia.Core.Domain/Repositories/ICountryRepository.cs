using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ICountryRepository : IRepository<Country, string>
{
    Task<bool> ExistsWithIsoCodeAsync(string isoCode, CancellationToken cancellationToken);
    Task<Country?> GetByIsoCodeAsync(string isoCode, CancellationToken cancellationToken);
    Task<bool> IsInUseAsync(string countryId, CancellationToken cancellationToken);
}