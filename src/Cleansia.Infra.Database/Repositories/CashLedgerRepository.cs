using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CashLedgerRepository(CleansiaDbContext context)
    : BaseRepository<CashLedgerEntry>(context), ICashLedgerRepository
{
    public async Task<IReadOnlyList<CashHeldBalance>> GetBalancesAsync(string? employeeId, CancellationToken cancellationToken)
    {
        var entries = GetQueryable();
        if (!string.IsNullOrEmpty(employeeId))
        {
            entries = entries.Where(e => e.EmployeeId == employeeId);
        }

        var balances = await entries
            .GroupBy(e => new
            {
                e.EmployeeId,
                e.Employee!.User!.FirstName,
                e.Employee.User.LastName,
                e.CurrencyId,
                e.Currency!.Code,
            })
            .Select(g => new
            {
                g.Key.EmployeeId,
                g.Key.FirstName,
                g.Key.LastName,
                g.Key.CurrencyId,
                g.Key.Code,
                Amount = g.Sum(e => e.Amount),
            })
            .Where(b => b.Amount != 0m)
            .OrderBy(b => b.LastName)
            .ThenBy(b => b.FirstName)
            .ThenBy(b => b.Code)
            .ToListAsync(cancellationToken);

        return balances
            .Select(b => new CashHeldBalance(
                b.EmployeeId, $"{b.FirstName} {b.LastName}".Trim(), b.CurrencyId, b.Code, b.Amount))
            .ToList();
    }
}
