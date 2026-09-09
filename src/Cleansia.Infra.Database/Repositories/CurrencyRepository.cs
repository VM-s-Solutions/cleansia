using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CurrencyRepository(CleansiaDbContext context) : BaseRepository<Currency>(context), ICurrencyRepository
{
    /// <summary>
    /// The platform's default currency. Throws when there is none — this sits on the most-travelled
    /// path in the platform (every quote and every order resolves it), so a null here becomes an NRE
    /// several frames away from the cause.
    ///
    /// <para>The previous form bound <c>??</c> to the <b>Task</b> rather than to its result. A Task
    /// returned by <c>FirstOrDefaultAsync</c> is never null, so the throw was unreachable and the
    /// <c>!</c> merely silenced the compiler while a null <c>Currency</c> escaped to the caller.</para>
    /// </summary>
    public async Task<Currency> GetDefaultAsync(CancellationToken cancellationToken)
    {
        return await GetDbSet().FirstOrDefaultAsync(c => c.IsDefault, cancellationToken)
               ?? throw new EntityNotFoundException("Default Currency was not found");
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

        // The catalogue price rows. Without these three, deleting a currency something is priced in
        // raises a raw 23503 at pipeline commit -- DeleteCurrency has no FK-violation mapping and no
        // early flush -- so the admin gets a 500 instead of currency.in_use.
        if (await Context.ServicePrices.AnyAsync(p => p.CurrencyId == currencyId, cancellationToken))
            return true;

        if (await Context.PackagePrices.AnyAsync(p => p.CurrencyId == currencyId, cancellationToken))
            return true;

        if (await Context.ExtraPrices.AnyAsync(p => p.CurrencyId == currencyId, cancellationToken))
            return true;

        return false;
    }
}