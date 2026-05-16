using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LanguageRepository(CleansiaDbContext context) : BaseRepository<Language>(context), ILanguageRepository
{
    public Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(l => l.Code == code, cancellationToken);
    }

    public Task<Language?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(l => l.Code == code, cancellationToken);
    }

    public async Task<bool> IsInUseAsync(string languageId, CancellationToken cancellationToken)
    {
        var language = await GetDbSet()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == languageId, cancellationToken);

        if (language is null)
            return false;

        // Check if language is used by Users (via PreferredLanguageCode which references Language.Code)
        if (await Context.Users.AnyAsync(u => u.PreferredLanguageCode == language.Code, cancellationToken))
            return true;

        if (await Context.EmailTranslations.AnyAsync(e => e.LanguageId == languageId, cancellationToken))
            return true;

        if (await Context.EmailTemplateTranslations.AnyAsync(e => e.LanguageId == languageId, cancellationToken))
            return true;

        if (await Context.OrderReceipts.AnyAsync(o => o.LanguageId == languageId, cancellationToken))
            return true;

        if (await Context.EmployeeInvoices.AnyAsync(e => e.LanguageId == languageId, cancellationToken))
            return true;

        return false;
    }
}