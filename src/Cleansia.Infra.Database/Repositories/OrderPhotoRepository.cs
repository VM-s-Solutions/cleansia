using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class OrderPhotoRepository(CleansiaDbContext context): BaseRepository<OrderPhoto>(context), IOrderPhotoRepository
{
    public Task<List<OrderPhoto>> GetPhotosByOrderIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(p => p.CapturedBy)
                .ThenInclude(e => e.User)
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.PhotoType)
            .ThenBy(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetPhotoCountByOrderIdAndTypeAsync(string orderId, PhotoType photoType, CancellationToken cancellationToken = default)
    {
        return GetDbSet().CountAsync(p => p.OrderId == orderId && p.PhotoType == photoType, cancellationToken);
    }
    // The customer id is read from the already-authorized order and checked again in the query.
    public Task<List<OrderPhoto>> GetPhotosByOrderIdForOwnerAsync(string orderId, string userId, CancellationToken cancellationToken)
        => GetQueryableIgnoringTenant().Include(p => p.CapturedBy).ThenInclude(e => e.User)
            .Where(p => p.OrderId == orderId && p.Order.UserId == userId)
            .OrderBy(p => p.PhotoType).ThenBy(p => p.CapturedAt).ToListAsync(cancellationToken);

    public Task<int> GetPhotoCountForOwnerAsync(string orderId, string userId, PhotoType photoType, CancellationToken cancellationToken)
        => GetQueryableIgnoringTenant().CountAsync(p => p.OrderId == orderId && p.Order.UserId == userId && p.PhotoType == photoType, cancellationToken);

    public async Task<IReadOnlyList<OrderPhoto>> GetPastRetentionAsync(
        string operatorTenantId, DateTime completedBefore, string? afterId, int take, CancellationToken cancellationToken)
    {
        var query = GetQueryableIgnoringTenant()
            .Where(p => p.Order.TenantId == operatorTenantId
                && p.Order.CompletedAt != null
                && p.Order.CompletedAt < completedBefore
                && !Context.Disputes.Any(d => d.OrderId == p.OrderId
                    && d.Status != DisputeStatus.Resolved
                    && d.Status != DisputeStatus.Closed));

        if (afterId is not null)
        {
            query = query.Where(p => string.Compare(p.Id, afterId) > 0);
        }

        return await query
            .OrderBy(p => p.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
