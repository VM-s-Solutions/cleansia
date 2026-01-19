namespace Cleansia.Core.AppServices.Features.ReceiptTemplates.DTOs;

public record ReceiptTemplateListItem(
    string Id,
    string TemplateName,
    string CountryId,
    string? CountryName,
    string LanguageId,
    string? LanguageCode,
    int Version,
    bool IsActive,
    DateTime? ActivatedAt,
    DateTimeOffset CreatedOn);