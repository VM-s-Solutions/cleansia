using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class LanguageMappers
{
    public static LanguageListItem MapToDto(this Language language)
    {
        return new LanguageListItem(
            language.Id,
            language.Code,
            language.Name);
    }

    public static LanguageDetailDto MapToDetailDto(this Language language)
    {
        return new LanguageDetailDto(
            language.Id,
            language.Code,
            language.Name);
    }
}