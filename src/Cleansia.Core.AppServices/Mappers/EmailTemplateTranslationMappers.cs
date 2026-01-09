using Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;
using Cleansia.Core.Domain.Emails;

namespace Cleansia.Core.AppServices.Mappers;

public static class EmailTemplateTranslationMappers
{
    public static EmailTemplateTranslationListItem MapToListItem(this EmailTemplateTranslation template)
    {
        return new EmailTemplateTranslationListItem(
            Id: template.Id,
            Key: template.Key,
            Value: template.Value,
            EmailType: template.EmailType,
            LanguageId: template.LanguageId,
            LanguageCode: template.Language?.Code,
            CreatedOn: template.CreatedOn,
            UpdatedOn: template.UpdatedOn);
    }

    public static EmailTemplateTranslationDetailDto MapToDetailDto(this EmailTemplateTranslation template)
    {
        return new EmailTemplateTranslationDetailDto(
            Id: template.Id,
            Key: template.Key,
            Value: template.Value,
            EmailType: template.EmailType,
            LanguageId: template.LanguageId,
            LanguageCode: template.Language?.Code,
            CreatedOn: template.CreatedOn,
            CreatedBy: template.CreatedBy,
            UpdatedOn: template.UpdatedOn,
            UpdatedBy: template.UpdatedBy);
    }
}