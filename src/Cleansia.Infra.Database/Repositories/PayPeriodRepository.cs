using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class PayPeriodRepository(CleansiaDbContext context) : BaseRepository<PayPeriod>(context), IPayPeriodRepository
{
    public Task<PayPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, cancellationToken);
    }

    public Task<PayPeriod?> GetActivePeriodAsync(CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(p => p.Status == PayPeriodStatus.Open)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsActivePeriodAsync(CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(p => p.Status == PayPeriodStatus.Open)
            .OrderByDescending(p => p.StartDate)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> HasOverlappingPeriodAsync(DateOnly startDate, DateOnly endDate, string? excludeId, CancellationToken cancellationToken)
    {
        var query = GetDbSet()
            .Where(p => p.StartDate <= endDate && p.EndDate >= startDate);

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            query = query.Where(p => p.Id != excludeId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<List<PayPeriod>> GetActivePeriodsEndingInDaysAsync(int daysFromNow, CancellationToken cancellationToken = default)
    {
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow));

        return GetDbSet()
            .Where(p => p.EndDate == targetDate && p.Status == PayPeriodStatus.Open)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<InvoiceReconciliationItem>> GetInvoiceReconciliationCandidatesAsync(
        DateTime olderThanUtc, int take, CancellationToken cancellationToken)
    {
        // ADR-0002 D3.4 — invoice-side dispatch reconciliation. System-job read: bypass the
        // tenant filter so the sweep sees stale pay across all tenants; each item carries its TenantId
        // so the sweep can re-scope per item before re-enqueuing.
        //
        // An employee is "in" a stale period iff they have an OrderEmployeePay row in a PayPeriod
        // committed BEFORE the cutoff; they are MISSING the invoice iff no EmployeeInvoice exists for
        // (PayPeriodId, EmployeeId). The anti-join is the LEFT-of-NOT-EXISTS; group/distinct collapses
        // the (possibly many) pay rows per (period, employee) to one re-enqueue candidate.
        var cutoff = new DateTimeOffset(olderThanUtc, TimeSpan.Zero);

        // ONE query rooted on the pay set with IgnoreQueryFilters so the ignore propagates across the
        // whole tree. Composing separate DbSets re-attaches each tenant filter, whose body is an
        // untranslatable provider call — so the stale-period filter goes through the NAVIGATION rather
        // than a second set, and the anti-join set is hoisted the same way.
        //
        // TenantId is in the grouping KEY rather than projected with Min(): all pay rows for one
        // (period, employee) share a tenant, so it does not change cardinality but does become
        // projectable. Order and bound on the key too — EF cannot sort by a property of the constructed
        // record.
        var existingInvoices = Context.Set<EmployeeInvoice>().IgnoreQueryFilters();

        var query =
            from pay in Context.Set<OrderEmployeePay>().IgnoreQueryFilters()
            where pay.PayPeriod!.CreatedOn <= cutoff
                && !existingInvoices.Any(inv =>
                    inv.PayPeriodId == pay.PayPeriodId && inv.EmployeeId == pay.EmployeeId)
            group pay by new { pay.PayPeriodId, pay.EmployeeId, pay.TenantId } into g
            orderby g.Key.PayPeriodId, g.Key.EmployeeId
            select new InvoiceReconciliationItem(
                g.Key.PayPeriodId,
                g.Key.EmployeeId,
                g.Key.TenantId);

        return await query
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
