using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

public interface ICashLedgerRepository : IRepository<CashLedgerEntry, string>
{
    /// <summary>
    /// The cash each of the company's cleaners holds, per currency, leaving out a currency they hold none
    /// of; one cleaner's alone when <paramref name="employeeId"/> is given.
    /// </summary>
    Task<IReadOnlyList<CashHeldBalance>> GetBalancesAsync(string? employeeId, CancellationToken cancellationToken);
}
