using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;

public record EmailTemplateTranslationListItem(
    string Id,
    string Key,
    string Value,
    EmailType EmailType,
    string LanguageId,
    string? LanguageCode,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);