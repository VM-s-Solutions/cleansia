using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IPayPeriodRepository : IRepository<PayPeriod, string>
{
    Task<PayPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken);
    Task<PayPeriod?> GetActivePeriodAsync(CancellationToken cancellationToken);
    Task<bool> ExistsActivePeriodAsync(CancellationToken cancellationToken);

    Task<bool> HasOverlappingPeriodAsync(DateOnly startDate, DateOnly endDate, string? excludeId, CancellationToken cancellationToken);

    Task<List<PayPeriod>> GetActivePeriodsEndingInDaysAsync(int daysFromNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-0002 D3.4 — invoice-side candidates for the dispatch reconciliation
    /// sweep: each (PayPeriod, Employee) pairing with committed pay (an <c>OrderEmployeePay</c> row in
    /// the period) but NO <c>EmployeeInvoice</c> for <c>(PayPeriodId, EmployeeId)</c>, where the period
    /// was committed BEFORE <paramref name="olderThanUtc"/>.
    ///
    /// <para>System-job read — bypasses the tenant filter (<c>IgnoreQueryFilters</c>) so the timer can
    /// sweep across all tenants; each item carries its <c>TenantId</c> so the sweep can set the override
    /// per item. Batch-bounded by <paramref name="take"/>.</para>
    /// </summary>
    Task<List<InvoiceReconciliationItem>> GetInvoiceReconciliationCandidatesAsync(
        DateTime olderThanUtc, int take, CancellationToken cancellationToken);
}
