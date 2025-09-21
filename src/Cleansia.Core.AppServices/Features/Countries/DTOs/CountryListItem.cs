using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Countries.DTOs;

public record CountryListItem(
    string Id,
    string Name,
    string IsoCode,
    Dictionary<string, Translation> Translations);