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

    public async Task<Device?> GetByUserAndDeviceIdIncludingInactiveAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        // Deliberately omits the `&& d.IsActive` predicate: registration must see a
        // logged-out tombstone to reclaim it, since (UserId, DeviceId) is uniquely
        // indexed across both active and inactive rows.
        return await context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId, cancellationToken);
    }

    public async Task<Device?> GetByIdAndUserAsync(string id, string userId, CancellationToken cancellationToken)
    {
        return await context.Devices
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && d.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await context.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);
    }
}
