using Cleansia.Core.AppServices.Features.InvoiceTemplates.DTOs;
using Cleansia.Core.Domain.InvoiceTemplates;

namespace Cleansia.Core.AppServices.Mappers;

public static class InvoiceTemplateMappers
{
    public static InvoiceTemplateListItem MapToListItem(this InvoiceTemplate template)
    {
        return new InvoiceTemplateListItem(
            Id: template.Id,
            TemplateName: template.TemplateName,
            CountryId: template.CountryId,
            CountryName: template.Country?.Name,
            LanguageId: template.LanguageId,
            LanguageCode: template.Language?.Code,
            Version: template.Version,
            IsActive: template.IsActive,
            ActivatedAt: template.ActivatedAt,
            CreatedOn: template.CreatedOn);
    }

    public static InvoiceTemplateDetailDto MapToDetailDto(this InvoiceTemplate template)
    {
        return new InvoiceTemplateDetailDto(
            Id: template.Id,
            TemplateName: template.TemplateName,
            CountryId: template.CountryId,
            CountryName: template.Country?.Name,
            LanguageId: template.LanguageId,
            LanguageCode: template.Language?.Code,
            Version: template.Version,
            BlobUrl: template.BlobUrl,
            IsActive: template.IsActive,
            ActivatedAt: template.ActivatedAt,
            Description: template.Description,
            CreatedOn: template.CreatedOn,
            CreatedBy: template.CreatedBy,
            UpdatedOn: template.UpdatedOn,
            UpdatedBy: template.UpdatedBy);
    }
}