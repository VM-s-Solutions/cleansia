namespace Cleansia.Core.AppServices.Features.InvoiceTemplates.DTOs;

public record InvoiceTemplateListItem(
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