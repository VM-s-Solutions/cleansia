namespace Cleansia.Core.AppServices.Features.Addresses.DTOs;

public record AddressDto(
    string Street,
    string City,
    string ZipCode,
    // Nullable: clients without a country picker (mobile booking) can omit
    // this and the CreateOrder handler resolves a default country (CZE, then
    // first configured) before persisting. See CreateOrder.cs ~line 219.
    string? CountryId,
    string? State);