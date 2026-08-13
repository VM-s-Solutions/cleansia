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

    /// <summary>
    /// The RevokedDeviceDirectory poll source (ADR-0026): every device deactivated at or after
    /// <paramref name="cutoff"/>, projected to <see cref="DeactivatedDevice"/>. The predicate is
    /// <c>DeactivatedOn &gt;= cutoff</c> ALONE — never conjoined with <c>IsActive == false</c>:
    /// <see cref="Device.MarkRegistered"/> reactivates a tombstone for any authenticated caller and
    /// never clears the stamp, so an IsActive filter would let a revoked device expunge its own
    /// directory entry by re-registering (ADR-0026 A1). The snapshot must be reactivation-insensitive
    /// — the token's iat guard alone decides session survival.
    /// </summary>
    Task<IReadOnlyList<DeactivatedDevice>> GetDeactivatedSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    /// GDPR erasure. Deletes every device row the subject holds, <b>ACTIVE OR NOT</b>.
    ///
    /// <para>That is the whole reason it exists: logout soft-deletes a device so the next login can
    /// reclaim the tombstone, and both paths that would otherwise remove it filter on <c>IsActive</c> — so
    /// a logged-out handset's user id, device id and push token were reachable by neither, indefinitely.
    /// A separate method rather than widening the existing one, whose other callers must keep seeing
    /// active rows only. → /flows/gdpr-and-audit</para>
    /// </summary>
    Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>Projection for the device-revocation poll (ADR-0026): the three fields the directory keys on.</summary>
public sealed record DeactivatedDevice(string UserId, string DeviceId, DateTimeOffset DeactivatedOn);
