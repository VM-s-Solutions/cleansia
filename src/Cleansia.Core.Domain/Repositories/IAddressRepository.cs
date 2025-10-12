using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IAddressRepository : IRepository<Address, string>
{
    Task<Address?> GetAddressAsync(string street, string city, string zipCode, string countryId, CancellationToken cancellationToken);
}