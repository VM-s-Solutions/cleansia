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
}
