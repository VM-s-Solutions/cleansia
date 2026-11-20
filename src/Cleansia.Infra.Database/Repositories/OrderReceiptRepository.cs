using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class OrderReceiptRepository(CleansiaDbContext context)
    : BaseRepository<OrderReceipt>(context), IOrderReceiptRepository
{

    public async Task<IReadOnlyList<OrderReceipt>> GetStoredBlobsIssuedBeforeAsync(
        DateTime issuedBefore, string? afterId, int take, CancellationToken cancellationToken)
    {
        var query = GetDbSet().Where(r => r.BlobDeletedAt == null && r.IssuedAt < issuedBefore);

        if (afterId is not null)
        {
            query = query.Where(r => string.Compare(r.Id, afterId) > 0);
        }

        return await query
            .OrderBy(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
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
        // Due is the whole test, code or no code: a stamped receipt is still due when the render after its
        // stamp was never stored (→ /flows/payment-and-fiscal#a-receipt-s-pdf-is-repaired). The partial index
        // IX_OrderReceipts_FiscalNextRetryAt (ON FiscalNextRetryAt IS NOT NULL) serves it.
        return await GetDbSet()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(r => r.Language)
            .Include(r => r.Receivable)
            .Where(r => r.FiscalNextRetryAt != null
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
