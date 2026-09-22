namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// Counts the ambient company's unsettled books in one read. Two callers: the lifecycle page shows
/// the facts, the archive request refuses on them.
/// </summary>
public interface ICompanySettlementReader
{
    Task<CompanySettlementFacts> ReadAsync(CancellationToken cancellationToken);
}
