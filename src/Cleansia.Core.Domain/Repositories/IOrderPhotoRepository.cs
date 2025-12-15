using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderPhotoRepository : IRepository<OrderPhoto, string>
{
    Task<List<OrderPhoto>> GetPhotosByOrderIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task<int> GetPhotoCountByOrderIdAndTypeAsync(string orderId, Cleansia.Core.Domain.Enums.PhotoType photoType, CancellationToken cancellationToken = default);
}
