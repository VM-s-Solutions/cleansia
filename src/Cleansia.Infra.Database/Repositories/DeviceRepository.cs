using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class DeviceRepository(CleansiaDbContext context) : BaseRepository<Device>(context), IDeviceRepository
{
    public async Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken)
    {
        return await context.Devices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.IsActive, cancellationToken);
    }

    public async Task<Device?> GetByUserAndDeviceIdAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        return await context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId && d.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await context.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);
    }
}
