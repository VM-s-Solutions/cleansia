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

    public Task<decimal> GetHeldAsync(string employeeId, string currencyId, CancellationToken cancellationToken) =>
        GetQueryable()
            .Where(e => e.EmployeeId == employeeId && e.CurrencyId == currencyId)
            .SumAsync(e => e.Amount, cancellationToken);

    public async Task<decimal> GetHeldUnderLockAsync(string employeeId, string currencyId, CancellationToken cancellationToken)
    {
        await context.LockCashHeldAsync(employeeId, currencyId, cancellationToken);
        return await GetHeldAsync(employeeId, currencyId, cancellationToken);
    }

    public async Task<bool> TryDebitAsync(CashLedgerEntry debit, CancellationToken cancellationToken)
    {
        await context.LockCashHeldAsync(debit.EmployeeId, debit.CurrencyId, cancellationToken);
        if (await GetHeldAsync(debit.EmployeeId, debit.CurrencyId, cancellationToken) + debit.Amount < 0m)
        {
            return false;
        }

        Add(debit);
        return true;
    }

    public async Task<IReadOnlyList<CashLedgerEntry>> GetForEmployeesAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken cancellationToken) =>
        await GetQueryable()
            .Where(e => employeeIds.Contains(e.EmployeeId))
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.CreatedOn)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
}
