using Cleansia.Core.AppServices.Features.ReceiptTemplates.DTOs;
using Cleansia.Core.Domain.ReceiptTemplates;

namespace Cleansia.Core.AppServices.Mappers;

public static class ReceiptTemplateMappers
{
    public static ReceiptTemplateListItem MapToListItem(this ReceiptTemplate template)
    {
        return new ReceiptTemplateListItem(
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

    public static ReceiptTemplateDetailDto MapToDetailDto(this ReceiptTemplate template)
    {
        return new ReceiptTemplateDetailDto(
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