using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class AddressRepository(CleansiaDbContext context) : BaseRepository<Address>(context), IAddressRepository
{
    public Task<Address?> GetAddressAsync(string street, string city, string zipCode, string countryId, CancellationToken cancellationToken)
    {
        return context.Addresses.FirstOrDefaultAsync(a =>
            a.Street == street &&
            a.City == city &&
            a.ZipCode == zipCode &&
            a.CountryId == countryId, cancellationToken);
    }
}