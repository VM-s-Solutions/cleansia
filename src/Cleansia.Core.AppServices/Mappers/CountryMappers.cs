using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class CountryMappers
{
    public static CountryListItem MapToDto(this Country country, bool isDefaultMarket = false) =>
        new(
            country.Id,
            country.IsoCode,
            country.IsoAlpha2,
            country.Name,
            Translations: country.Translations.ToDictionary(),
            IsDefaultMarket: isDefaultMarket);

    public static CountryDetailDto MapToDetailDto(this Country country, CountryConfiguration? configuration = null) =>
        new(
            country.Id,
            country.IsoCode,
            country.IsoAlpha2,
            country.Name,
            country.IsServiced,
            InsuranceCoverageAmount: configuration?.InsuranceCoverageAmount,
            HasConfiguration: configuration is not null,
            IsDefaultMarket: configuration?.IsDefaultMarket ?? false);

    public static ServiceAreaCountryDto MapToServiceAreaDto(this Country country, CountryConfiguration? configuration) =>
        new(
            country.Id,
            country.IsoCode,
            country.IsoAlpha2,
            country.Name,
            Translations: country.Translations.ToDictionary(),
            // The admin switch's own column, not the effective "serviced" predicate (CountryRepository.GetServicedAsync). → /architecture/security-rules#s10-soft-delete-isactive-semantics
            IsServiced: country.IsServiced,
            IsDefaultMarket: configuration?.IsDefaultMarket ?? false,
            HasConfiguration: configuration is not null);
}