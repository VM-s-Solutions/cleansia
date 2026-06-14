using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.SavedAddresses.Mappers;

public static class SavedAddressMapper
{
    public static SavedAddressDto MapToDto(this SavedAddress saved, Address address, string? countryName) =>
        new(
            Id: saved.Id,
            Label: saved.Label,
            Street: address.Street,
            City: address.City,
            ZipCode: address.ZipCode,
            State: address.State,
            CountryId: address.CountryId,
            Country: countryName,
            Latitude: address.Latitude,
            Longitude: address.Longitude,
            IsDefault: saved.IsDefault);

    public static SavedAddressDto MapToDto(this SavedAddress saved) =>
        saved.MapToDto(saved.Address!, saved.Address!.Country?.Name);
}
