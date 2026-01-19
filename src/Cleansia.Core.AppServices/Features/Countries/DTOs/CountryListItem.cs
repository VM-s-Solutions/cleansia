using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Countries.DTOs;

public record CountryListItem(
    string Id,
    string IsoCode,
    string Name,
    Dictionary<string, Translation> Translations);