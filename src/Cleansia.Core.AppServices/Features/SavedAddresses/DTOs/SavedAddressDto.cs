namespace Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;

public record SavedAddressDto(
    string Id,
    string Label,
    string Street,
    string City,
    string ZipCode,
    string? State,
    string CountryId,
    string? Country,
    double? Latitude,
    double? Longitude,
    bool IsDefault);
