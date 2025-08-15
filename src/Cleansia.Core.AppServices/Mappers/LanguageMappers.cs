using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Core.Domain.Internalization;

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
}