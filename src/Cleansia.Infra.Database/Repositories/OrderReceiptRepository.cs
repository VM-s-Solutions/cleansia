using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class OrderReceiptRepository(CleansiaDbContext context)
    : BaseRepository<OrderReceipt>(context), IOrderReceiptRepository
{
    public async Task<OrderReceipt?> GetByOrderIdAndLanguageAsync(
        string orderId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(r => r.Order)
            .Include(r => r.Language)
            .FirstOrDefaultAsync(
                r => r.OrderId == orderId && r.Language!.Code == languageCode,
                cancellationToken);
    }

    public async Task<List<OrderReceipt>> GetByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(r => r.Order)
            .Include(r => r.Language)
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OrderReceipt>> GetDueForRetryAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken)
    {
        // System-job query — bypasses tenant filter so the timer can pick up
        // failed receipts across all tenants. Caller MUST set
        // ITenantProvider.SetTenantOverride(receipt.TenantId) per row before
        // any subsequent mutation so child writes (status updates, emails)
        // inherit the right tenant.
        // ADR-0004 C-A — sweep BOTH residuals due for retry:
        //  (1) the existing failed-registration rows (FiscalRegistrationFailed == true), AND
        //  (2) the born-retry-eligible CLAIMED-BUT-UNREGISTERED rows that claim-before-register
        //      introduces: a crash between the phase-1 claim commit and the authority register leaves
        //      FiscalRegistrationFailed == false, FiscalCode == null, FiscalNextRetryAt set. Without
        //      this arm those rows were INVISIBLE to the retry job (a silently-unregistered sale).
        // The unifying condition is "not yet registered (FiscalCode == null) and scheduled to retry",
        // which subsumes (1) — a failed registration also has FiscalCode == null — so the widened
        // predicate covers both. The existing partial filtered index IX_OrderReceipts_FiscalNextRetryAt
        // (ON FiscalNextRetryAt IS NOT NULL) already supports it → no migration.
        return await GetDbSet()
            .IgnoreQueryFilters()
            .Include(r => r.Language)
            .Where(r => r.FiscalCode == null
                && r.FiscalNextRetryAt != null
                && r.FiscalNextRetryAt <= utcNow)
            .OrderBy(r => r.FiscalNextRetryAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OrderReceipt>> GetRecentFiscalFailuresAsync(
        int take,
        CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(r => r.Order)
            .Where(r => r.FiscalRegistrationFailed && !r.FiscalAcknowledged)
            .OrderByDescending(r => r.FiscalLastRetryAt ?? r.IssuedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
