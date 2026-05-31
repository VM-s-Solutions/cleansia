namespace Cleansia.Core.AppServices.Features.Countries.DTOs;

public record CountryDetailDto(
    string Id,
    string IsoCode,
    string Name,
    bool IsServiced = false);