using Cleansia.Core.Domain.InvoiceTemplates;

namespace Cleansia.Core.Domain.Repositories;

public interface IInvoiceTemplateRepository : IRepository<InvoiceTemplate, string>
{
    Task<InvoiceTemplate?> GetActiveByCountryAndLanguageAsync(string? countryId, string languageCode, CancellationToken cancellationToken);
    IQueryable<InvoiceTemplate> GetByCountry(string countryId);
    IQueryable<InvoiceTemplate> GetActiveTemplates();
    Task<int> GetNextVersionAsync(string countryId, string languageId, CancellationToken cancellationToken);
    Task<bool> ExistsActiveTemplateAsync(string countryId, string languageId, CancellationToken cancellationToken);
}
