using Cleansia.Core.Domain.InvoiceTemplates;

namespace Cleansia.Core.Domain.Repositories;

public interface ICountryInvoiceConfigRepository : IRepository<CountryInvoiceConfig, string>
{
    Task<CountryInvoiceConfig?> GetByCountryIdAsync(string countryId, CancellationToken cancellationToken);
    Task<bool> ExistsByCountryIdAsync(string countryId, CancellationToken cancellationToken);
}
