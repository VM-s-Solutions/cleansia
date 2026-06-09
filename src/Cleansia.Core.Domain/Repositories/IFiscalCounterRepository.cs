using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.Domain.Repositories;

public interface IFiscalCounterRepository : IRepository<FiscalCounter, string>
{
    /// <summary>
    /// Atomically allocates and returns the next fiscal number for
    /// <c>(currentTenant, year, issuerScope)</c>. The increment runs on the context's current
    /// connection, so it joins the same transaction the caller commits the receipt claim in — a
    /// committed claim never holds a number that was rolled back, and a rolled-back claim returns its
    /// number to the pool without shifting the next allocation. Concurrent callers are serialized on
    /// the counter row, so N concurrent allocations yield N distinct contiguous numbers.
    /// </summary>
    Task<long> AllocateNextAsync(int year, string issuerScope, CancellationToken cancellationToken);
}
