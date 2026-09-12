using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class CountryMappers
{
    public static CountryListItem MapToDto(this Country country) =>
        new(
            country.Id,
            country.IsoCode,
            country.IsoAlpha2,
            country.Name,
            Translations: country.Translations.ToDictionary());

    public static CountryDetailDto MapToDetailDto(this Country country, CountryConfiguration? configuration = null) =>
        new(
            country.Id,
            country.IsoCode,
            country.IsoAlpha2,
            country.Name,
            country.IsServiced,
            InsuranceCoverageAmount: configuration?.InsuranceCoverageAmount,
            HasConfiguration: configuration is not null);
}