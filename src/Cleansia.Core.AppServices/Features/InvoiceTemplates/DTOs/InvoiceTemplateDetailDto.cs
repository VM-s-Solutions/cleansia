namespace Cleansia.Core.AppServices.Features.InvoiceTemplates.DTOs;

public record InvoiceTemplateDetailDto(
    string Id,
    string TemplateName,
    string CountryId,
    string? CountryName,
    string LanguageId,
    string? LanguageCode,
    int Version,
    string BlobUrl,
    bool IsActive,
    DateTime? ActivatedAt,
    string? Description,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset? UpdatedOn,
    string? UpdatedBy);