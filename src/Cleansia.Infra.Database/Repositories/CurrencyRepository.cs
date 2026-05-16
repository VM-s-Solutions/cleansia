using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CurrencyRepository(CleansiaDbContext context) : BaseRepository<Currency>(context), ICurrencyRepository
{
    public Task<Currency> GetDefaultAsync(CancellationToken cancellationToken)
    {
        return (GetDbSet().FirstOrDefaultAsync(c => c.IsDefault, cancellationToken) ??
                throw new EntityNotFoundException("Default Currency was not found"))!;
    }

    public Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<bool> IsInUseAsync(string currencyId, CancellationToken cancellationToken)
    {
        if (await Context.Orders.AnyAsync(o => o.CurrencyId == currencyId, cancellationToken))
            return true;

        if (await Context.EmployeePayConfigs.AnyAsync(p => p.CurrencyId == currencyId, cancellationToken))
            return true;

        if (await Context.EmployeeInvoices.AnyAsync(i => i.CurrencyId == currencyId, cancellationToken))
            return true;

        return false;
    }
}