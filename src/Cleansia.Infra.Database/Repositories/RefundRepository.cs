using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class RefundRepository(CleansiaDbContext context) : BaseRepository<Refund>(context), IRefundRepository
{
    public Task<Refund?> GetByRefundKeyAsync(string refundKey, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(r => r.RefundKey == refundKey, cancellationToken);
    }

    public async Task<decimal> GetSucceededRefundTotalForOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.OrderId == orderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetSucceededRefundTotalsByOrderAsync(
        IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<string, decimal>(0);
        }

        var rows = await GetDbSet()
            .Where(r => r.Status == RefundStatus.Succeeded && orderIds.Contains(r.OrderId))
            .GroupBy(r => r.OrderId)
            .Select(g => new { OrderId = g.Key, Total = g.Sum(r => r.Amount) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.OrderId, r => r.Total);
    }
}
