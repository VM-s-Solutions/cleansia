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
}
