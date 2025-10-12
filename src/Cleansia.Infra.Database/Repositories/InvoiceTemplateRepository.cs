using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class InvoiceTemplateRepository(CleansiaDbContext context) : BaseRepository<InvoiceTemplate>(context), IInvoiceTemplateRepository
{
    public Task<InvoiceTemplate?> GetActiveByCountryAndLanguageAsync(string countryId, string languageId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(t => t.Language)
            .Where(t => t.CountryId == countryId && t.Language.Code == languageId && t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public IQueryable<InvoiceTemplate> GetByCountry(string countryId)
    {
        return GetDbSet()
            .Where(t => t.CountryId == countryId)
            .OrderByDescending(t => t.Version);
    }

    public IQueryable<InvoiceTemplate> GetActiveTemplates()
    {
        return GetDbSet()
            .Where(t => t.IsActive)
            .OrderBy(t => t.CountryId)
            .ThenBy(t => t.LanguageId);
    }

    public async Task<int> GetNextVersionAsync(string countryId, string languageId, CancellationToken cancellationToken)
    {
        var maxVersion = await GetDbSet()
            .Where(t => t.CountryId == countryId && t.LanguageId == languageId)
            .MaxAsync(t => (int?)t.Version, cancellationToken);

        return (maxVersion ?? 0) + 1;
    }

    public Task<bool> ExistsActiveTemplateAsync(string countryId, string languageId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(t => t.CountryId == countryId && t.LanguageId == languageId && t.IsActive, cancellationToken);
    }
}
