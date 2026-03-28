using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CountryRepository(CleansiaDbContext context) : BaseRepository<Country>(context), ICountryRepository
{
    public Task<bool> ExistsWithIsoCodeAsync(string isoCode, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(c => c.IsoCode == isoCode, cancellationToken);
    }

    public Task<Country?> GetByIsoCodeAsync(string isoCode, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(c => c.IsoCode == isoCode, cancellationToken);
    }

    public async Task<bool> IsInUseAsync(string countryId, CancellationToken cancellationToken)
    {
        // Check if country is used by Addresses (via Employee)
        if (await Context.Employees.AnyAsync(e => e.Address != null && e.Address.CountryId == countryId, cancellationToken))
            return true;

        // Check if country is used by CompanyInfo
        if (await Context.CompanyInfo.AnyAsync(c => c.CountryId == countryId, cancellationToken))
            return true;

        // Check if country is used by CountryInvoiceConfigs
        if (await Context.CountryInvoiceConfigs.AnyAsync(c => c.CountryId == countryId, cancellationToken))
            return true;

        // Check if country is used by EmployeeInvoices
        if (await Context.EmployeeInvoices.AnyAsync(e => e.CountryId == countryId, cancellationToken))
            return true;

        return false;
    }
}