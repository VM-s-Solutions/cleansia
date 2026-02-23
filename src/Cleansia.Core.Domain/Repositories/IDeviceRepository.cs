using Cleansia.Core.Domain.Devices;

namespace Cleansia.Core.Domain.Repositories;

public interface IDeviceRepository : IRepository<Device, string>
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken);
    Task<Device?> GetByUserAndDeviceIdAsync(string userId, string deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Device>> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
}
