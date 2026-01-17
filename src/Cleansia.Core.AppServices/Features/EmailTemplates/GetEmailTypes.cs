using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class GetEmailTypes
{
    public record Query : IQuery<List<EmailTypeListItemDto>>;

    internal class Handler(IEmailTemplateTranslationRepository repository)
        : IQueryHandler<Query, List<EmailTypeListItemDto>>
    {
        private static readonly Dictionary<EmailType, string> EmailTypeDisplayNames = new()
        {
            { EmailType.ConfirmationEmail, "Confirmation Email" },
            { EmailType.ResetPassword, "Reset Password" },
            { EmailType.OrderReceipt, "Order Receipt" },
            { EmailType.PeriodClosed, "Period Closed" },
            { EmailType.PeriodEndReminder, "Period End Reminder" }
        };

        public async Task<BusinessResult<List<EmailTypeListItemDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var allTemplates = await repository.GetAll()
                .Include(e => e.Language).ToListAsync(cancellationToken);

            var groupedByType = allTemplates
                .GroupBy(t => t.EmailType)
                .Select(g => new EmailTypeListItemDto(
                    g.Key,
                    EmailTypeDisplayNames.GetValueOrDefault(g.Key, g.Key.ToString()),
                    g.Count(),
                    g.Where(t => t.Language != null)
                     .Select(t => t.Language!.Code)
                     .Distinct()
                     .OrderBy(code => code)
                     .ToList(),
                    g.Max(t => t.UpdatedOn ?? t.CreatedOn)
                ))
                .OrderBy(dto => dto.EmailType)
                .ToList();

            // Add missing email types with 0 translations
            var existingTypes = groupedByType.Select(g => g.EmailType).ToHashSet();
            foreach (var emailType in Enum.GetValues<EmailType>())
            {
                if (!existingTypes.Contains(emailType))
                {
                    groupedByType.Add(new EmailTypeListItemDto(
                        emailType,
                        EmailTypeDisplayNames.GetValueOrDefault(emailType, emailType.ToString()),
                        0,
                        new List<string>(),
                        null
                    ));
                }
            }

            return BusinessResult.Success(groupedByType.OrderBy(dto => dto.EmailType).ToList());
        }
    }
}