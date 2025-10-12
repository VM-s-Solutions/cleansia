namespace Cleansia.Core.AppServices.Features.Addresses.DTOs;

public record AddressDto(
    string Street,
    string City,
    string ZipCode,
    string CountryId);