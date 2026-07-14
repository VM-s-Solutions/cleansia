using Cleansia.Core.Domain.Devices;

namespace Cleansia.Core.Domain.Repositories;

public interface IDeviceRepository : IRepository<Device, string>
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken);
    Task<Device?> GetByUserAndDeviceIdAsync(string userId, string deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the device by (userId, deviceId) INCLUDING soft-deleted (IsActive=false)
    /// rows, so registration can reclaim a logged-out tombstone instead of colliding
    /// with the unique index on re-registration. Register-path only.
    /// </summary>
    Task<Device?> GetByUserAndDeviceIdIncludingInactiveAsync(string userId, string deviceId, CancellationToken cancellationToken);
    Task<Device?> GetByIdAndUserAsync(string id, string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Device>> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
}
