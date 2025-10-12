using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmailTranslationRepository(CleansiaDbContext context): BaseRepository<EmailTranslation>(context), IEmailTranslationRepository
{
    public Task<EmailTranslation?> GetByLanguageCodeAndTypeAsync(string languageCode, EmailType emailType, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(translation => translation.Language)
            .FirstOrDefaultAsync(t => t.Language.Code == languageCode && t.EmailType == emailType, cancellationToken);
    }
}