namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// Counts the ambient company's unsettled books in one read. Two callers: the lifecycle page shows
/// the facts, the archive request refuses on them.
/// </summary>
public interface ICompanySettlementReader
{
    Task<CompanySettlementFacts> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The two pay facts of <see cref="ReadAsync"/> for one pay period, under the same predicates: invoices
    /// neither paid nor cancelled, and pay rows no invoice holds. Marking the period paid refuses on them.
    /// </summary>
    Task<(int UnpaidInvoices, int UninvoicedPayRows)> ReadPayPeriodAsync(string payPeriodId, CancellationToken cancellationToken);
}
