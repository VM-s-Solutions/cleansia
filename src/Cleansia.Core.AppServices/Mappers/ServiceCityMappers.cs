using Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;
using Cleansia.Core.Domain.ServiceAreas;

namespace Cleansia.Core.AppServices.Mappers;

public static class ServiceCityMappers
{
    public static ServiceCityDto MapToDto(this ServiceCity city) =>
        new(
            Id: city.Id,
            CountryId: city.CountryId,
            CountryName: city.Country?.Name ?? string.Empty,
            CountryIsoCode: city.Country?.IsoCode ?? string.Empty,
            Name: city.Name,
            ZipPrefix: city.ZipPrefix,
            IsActive: city.IsActive);
}
