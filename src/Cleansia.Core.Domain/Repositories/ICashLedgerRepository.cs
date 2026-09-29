using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

public interface ICashLedgerRepository : IRepository<CashLedgerEntry, string>
{
    /// <summary>
    /// The cash each of the company's cleaners holds, per currency, leaving out a currency they hold none
    /// of; one cleaner's alone when <paramref name="employeeId"/> is given.
    /// </summary>
    Task<IReadOnlyList<CashHeldBalance>> GetBalancesAsync(string? employeeId, CancellationToken cancellationToken);

    /// <summary>The cash one cleaner holds in one currency.</summary>
    Task<decimal> GetHeldAsync(string employeeId, string currencyId, CancellationToken cancellationToken);

    /// <summary>Every entry of the given cleaners, tracked, oldest first.</summary>
    Task<IReadOnlyList<CashLedgerEntry>> GetForEmployeesAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken cancellationToken);
}
