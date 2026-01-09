using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class CountryMappers
{
    public static CountryListItem MapToDto(this Country country) =>
        new(
            country.Id,
            country.IsoCode,
            country.Name);

    public static CountryDetailDto MapToDetailDto(this Country country) =>
        new(
            country.Id,
            country.IsoCode,
            country.Name);
}