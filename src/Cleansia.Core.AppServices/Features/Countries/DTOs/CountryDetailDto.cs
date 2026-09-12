namespace Cleansia.Core.AppServices.Features.Countries.DTOs;

public record CountryDetailDto(
    string Id,
    string IsoCode,
    string IsoAlpha2,
    string Name,
    bool IsServiced = false,
    decimal? InsuranceCoverageAmount = null,
    bool HasConfiguration = false);