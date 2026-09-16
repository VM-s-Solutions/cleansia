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
        if (await Context.Employees.AnyAsync(e => e.Address != null && e.Address.CountryId == countryId, cancellationToken))
            return true;

        if (await Context.CompanyInfo.AnyAsync(c => c.CountryId == countryId, cancellationToken))
            return true;

        if (await Context.CountryInvoiceConfigs.AnyAsync(c => c.CountryId == countryId, cancellationToken))
            return true;

        if (await Context.EmployeeInvoices.AnyAsync(e => e.CountryId == countryId, cancellationToken))
            return true;

        return false;
    }

    // A country is open for business only while an operating company serves it (ADR-0064 D1): the
    // fourth market predicate, held in these two reads so every caller of "serviced" inherits it. This
    // is the one place the column behind Tenant.IsDeactivated is read directly — a computed predicate
    // does not translate to SQL. Configurations are tenantless, so the join sees every company's markets.
    public async Task<IReadOnlyList<Country>> GetServicedAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(c => c.IsServiced && c.IsActive
                && Context.CountryConfigurations.Any(cc =>
                    cc.CountryId == c.Id && cc.OperatorTenant != null && cc.OperatorTenant.IsActive))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsServicedAsync(string countryId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(
            c => c.Id == countryId && c.IsServiced && c.IsActive
                && Context.CountryConfigurations.Any(cc =>
                    cc.CountryId == c.Id && cc.OperatorTenant != null && cc.OperatorTenant.IsActive),
            cancellationToken);
    }
}