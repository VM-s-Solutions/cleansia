using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IPayoutReferenceCounterRepository : IRepository<PayoutReferenceCounter, string>
{
    /// <summary>
    /// Atomically allocates and returns the next payout-reference ordinal for <paramref name="year"/>,
    /// or <see langword="null"/> when that year's capacity (999 999) is exhausted. Concurrent callers
    /// are serialized on the single counter row, so N concurrent allocations yield N distinct values.
    ///
    /// <para>The allocation SELF-COMMITS and is NOT rolled back with the caller's unit of work, so a
    /// caller whose invoice never commits simply leaves a gap. That is intended — see
    /// <see cref="PayoutReferenceCounter"/>. It also means the caller MUST NOT invoke this inside an
    /// explicit transaction (ADR-0046 §D2.1): the statement would join it, the gap semantics would
    /// become "unless the caller opened a transaction", and the row lock on the single counter row
    /// would serialize every concurrent payroll run for the life of that transaction.</para>
    /// </summary>
    Task<long?> AllocateNextAsync(int year, CancellationToken cancellationToken);
}
