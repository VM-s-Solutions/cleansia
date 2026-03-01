using Cleansia.Core.Domain.ReceiptTemplates;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ReceiptTemplateRepository(CleansiaDbContext context)
    : BaseRepository<ReceiptTemplate>(context), IReceiptTemplateRepository
{
    public async Task<ReceiptTemplate?> GetActiveByCountryAndLanguageAsync(
        string? countryId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var query = GetDbSet()
            .Include(t => t.Country)
            .Include(t => t.Language)
            .Where(t => t.Language!.Code == languageCode && t.IsActive);

        if (!string.IsNullOrEmpty(countryId))
            query = query.Where(t => t.CountryId == countryId);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetNextVersionAsync(
        string countryId,
        string languageId,
        CancellationToken cancellationToken)
    {
        var maxVersion = await GetDbSet()
            .Where(t => t.CountryId == countryId && t.LanguageId == languageId)
            .MaxAsync(t => (int?)t.Version, cancellationToken);

        return (maxVersion ?? 0) + 1;
    }

    public async Task<bool> ExistsActiveTemplateAsync(
        string countryId,
        string languageId,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .AnyAsync(
                t => t.CountryId == countryId
                     && t.LanguageId == languageId
                     && t.IsActive,
                cancellationToken);
    }
}
