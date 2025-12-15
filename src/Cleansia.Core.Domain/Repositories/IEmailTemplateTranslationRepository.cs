using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmailTemplateTranslationRepository : IRepository<EmailTemplateTranslation, string>
{
    Task<Dictionary<string, string>> GetTranslationsByTypeAndLanguageAsync(
        EmailType emailType,
        string languageCode,
        CancellationToken cancellationToken);

    Task<List<EmailTemplateTranslation>> GetByEmailTypeAsync(
        EmailType emailType,
        CancellationToken cancellationToken);
}
