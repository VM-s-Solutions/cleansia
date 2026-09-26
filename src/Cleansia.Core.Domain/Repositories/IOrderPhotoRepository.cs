using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderPhotoRepository : IRepository<OrderPhoto, string>
{
    Task<List<OrderPhoto>> GetPhotosByOrderIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task<int> GetPhotoCountByOrderIdAndTypeAsync(string orderId, Cleansia.Core.Domain.Enums.PhotoType photoType, CancellationToken cancellationToken = default);
    Task<List<OrderPhoto>> GetPhotosByOrderIdForOwnerAsync(string orderId, string userId, CancellationToken cancellationToken);
    Task<int> GetPhotoCountForOwnerAsync(string orderId, string userId, Cleansia.Core.Domain.Enums.PhotoType photoType, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the photos on <paramref name="operatorTenantId"/>'s orders completed before
    /// <paramref name="completedBefore"/>, ordered by id and starting after <paramref name="afterId"/>.
    /// An order with a dispute still open keeps its photos, because they are the dispute's evidence. Read
    /// past the tenant filter and pinned to the company by the ORDER: a photo carries its uploader's stamp
    /// and a dispute its order's operator.
    /// </summary>
    Task<IReadOnlyList<OrderPhoto>> GetPastRetentionAsync(
        string operatorTenantId, DateTime completedBefore, string? afterId, int take, CancellationToken cancellationToken);
}
