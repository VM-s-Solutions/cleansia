using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;

public record EmailTypeListItemDto(
    EmailType EmailType,
    string DisplayName,
    int TranslationCount,
    List<string> AvailableLanguages,
    DateTimeOffset? LastModified);