using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmailTemplateTranslationRepository(CleansiaDbContext context)
    : BaseRepository<EmailTemplateTranslation>(context), IEmailTemplateTranslationRepository
{
    public async Task<Dictionary<string, string>> GetTranslationsByTypeAndLanguageAsync(
        EmailType emailType,
        string languageCode,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(t => t.Language)
            .Where(t => t.EmailType == emailType && t.Language!.Code == languageCode)
            .ToDictionaryAsync(t => t.Key, t => t.Value, cancellationToken);
    }

    public async Task<List<EmailTemplateTranslation>> GetByEmailTypeAsync(
        EmailType emailType,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(t => t.Language)
            .Where(t => t.EmailType == emailType)
            .ToListAsync(cancellationToken);
    }
}
