using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CountryInvoiceConfigRepository(CleansiaDbContext context) : BaseRepository<CountryInvoiceConfig>(context), ICountryInvoiceConfigRepository
{
    public Task<CountryInvoiceConfig?> GetByCountryIdAsync(string countryId, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(c => c.CountryId == countryId, cancellationToken);
    }

    public Task<bool> ExistsByCountryIdAsync(string countryId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(c => c.CountryId == countryId, cancellationToken);
    }
}
