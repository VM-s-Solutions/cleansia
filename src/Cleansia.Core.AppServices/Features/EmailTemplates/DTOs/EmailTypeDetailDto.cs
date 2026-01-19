using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;

public record EmailTypeDetailDto(
    EmailType EmailType,
    string DisplayName,
    List<EmailTranslationByLanguageDto> Translations);

public record EmailTranslationByLanguageDto(
    string LanguageId,
    string LanguageCode,
    string LanguageName,
    List<EmailTemplateKeyValueDto> KeyValues);

public record EmailTemplateKeyValueDto(
    string Id,
    string Key,
    string Value,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset? UpdatedOn,
    string? UpdatedBy);