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
}
