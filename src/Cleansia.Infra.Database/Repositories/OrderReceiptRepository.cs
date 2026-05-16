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

    public async Task<int> GetNextSequenceForYearAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var startOfYear = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfYear = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var count = await GetDbSet()
            .Where(r => r.IssuedAt >= startOfYear && r.IssuedAt <= endOfYear)
            .CountAsync(cancellationToken);

        return count + 1;
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
        return await GetDbSet()
            .IgnoreQueryFilters()
            .Include(r => r.Language)
            .Where(r => r.FiscalRegistrationFailed
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
