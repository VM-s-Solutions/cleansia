using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmailTranslationRepository : IRepository<EmailTranslation, string>
{
    Task<EmailTranslation?> GetByLanguageCodeAndTypeAsync(string languageCode, EmailType emailType, CancellationToken cancellationToken);
}