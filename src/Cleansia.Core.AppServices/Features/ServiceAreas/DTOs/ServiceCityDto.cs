namespace Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;

public record ServiceCityDto(
    string Id,
    string CountryId,
    string CountryName,
    string CountryIsoCode,
    string Name,
    string? ZipPrefix,
    bool IsActive);
