using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class DeviceRepository(CleansiaDbContext context, IUserSessionProvider userSessionProvider)
    : BaseRepository<Device>(context), IDeviceRepository
{
    private const string SystemActor = "System";

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

    public async Task<IReadOnlyList<DeactivatedDevice>> GetDeactivatedSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        // Background, tenant-less, cross-tenant-by-design read (the sanctioned T-0245 pattern): the
        // directory refresher has no JWT context and device ids are globally unique, so IgnoreQueryFilters
        // is required and safe. Predicate is DeactivatedOn >= cutoff ALONE (no IsActive conjunct) so a
        // re-registered tombstone cannot expunge its own revocation entry (ADR-0026 A1).
        return await context.Devices
            .IgnoreQueryFilters()
            .Where(d => d.DeactivatedOn != null && d.DeactivatedOn >= cutoff)
            .Select(d => new DeactivatedDevice(d.UserId, d.DeviceId, d.DeactivatedOn!.Value))
            .ToListAsync(cancellationToken);
    }

    public override void Deactivate(Device entity)
    {
        var actorId = userSessionProvider?.GetUserId();
        var deactivatedBy = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId!;
        entity.Deactivated(deactivatedBy, DateTimeOffset.UtcNow);
    }
}
