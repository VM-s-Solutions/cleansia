using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Geocoding;

namespace Cleansia.Core.AppServices.Services;

public class AddressGeocoder(
    IGeocodingService geocodingService,
    ICountryRepository countryRepository) : IAddressGeocoder
{
    public async Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
    {
        var country = await countryRepository.GetByIdAsync(address.CountryId, cancellationToken);

        var coordinates = await geocodingService.GeocodeAsync(
            address.Street,
            address.City,
            address.ZipCode,
            country?.IsoCode,
            cancellationToken);

        if (coordinates == null)
        {
            return;
        }

        address.Update(
            address.Street,
            address.City,
            address.ZipCode,
            address.CountryId,
            address.State,
            coordinates.Latitude,
            coordinates.Longitude);
    }
}
