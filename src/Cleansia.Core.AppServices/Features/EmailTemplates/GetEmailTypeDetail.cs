using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class GetEmailTypeDetail
{
    public record Query(EmailType EmailType) : IQuery<EmailTypeDetailDto>;

    private static readonly Dictionary<EmailType, string> EmailTypeDisplayNames = new()
    {
        { EmailType.ConfirmationEmail, "Confirmation Email" },
        { EmailType.ResetPassword, "Reset Password" },
        { EmailType.OrderReceipt, "Order Receipt" },
        { EmailType.PeriodClosed, "Period Closed" },
        { EmailType.PeriodEndReminder, "Period End Reminder" }
    };

    internal class Handler(IEmailTemplateTranslationRepository repository)
        : IQueryHandler<Query, EmailTypeDetailDto>
    {
        public async Task<BusinessResult<EmailTypeDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var templates = await repository.GetByEmailTypeAsync(query.EmailType, cancellationToken);

            var translationsByLanguage = templates
                .Where(t => t.Language != null)
                .GroupBy(t => new { t.LanguageId, t.Language!.Code, t.Language.Name })
                .Select(g => new EmailTranslationByLanguageDto(
                    g.Key.LanguageId,
                    g.Key.Code,
                    g.Key.Name,
                    g.Select(t => new EmailTemplateKeyValueDto(
                        t.Id,
                        t.Key,
                        t.Value,
                        t.CreatedOn,
                        t.CreatedBy ?? "",
                        t.UpdatedOn,
                        t.UpdatedBy
                    )).OrderBy(kv => kv.Key).ToList()
                ))
                .OrderBy(l => l.LanguageCode)
                .ToList();

            return BusinessResult.Success(new EmailTypeDetailDto(
                query.EmailType,
                EmailTypeDisplayNames.GetValueOrDefault(query.EmailType, query.EmailType.ToString()),
                translationsByLanguage
            ));
        }
    }
}