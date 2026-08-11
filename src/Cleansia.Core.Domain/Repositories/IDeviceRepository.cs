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
    /// GDPR erasure. Deletes every device row the subject holds, ACTIVE OR NOT.
    ///
    /// <para>The <c>IsActive</c> half is the whole reason this exists rather than reusing
    /// <see cref="GetByUserIdAsync"/>: logout soft-deletes a device and leaves the row physically present so
    /// the next login can reclaim the tombstone, and BOTH paths that would otherwise remove it filter on
    /// <c>IsActive</c> — the erasure's own read did, and the stale-device retention sweep still does
    /// (<c>device.IsActive &amp;&amp; LastActiveAt &lt; cutoff</c>). A logged-out handset's <c>UserId</c>,
    /// <c>DeviceId</c> and push token were therefore reachable by neither, indefinitely.</para>
    ///
    /// <para>A separate method rather than widening <see cref="GetByUserIdAsync"/>: that one's other callers
    /// (the "my devices" list, push targeting) must keep seeing active rows only, and one method deciding
    /// that on its callers' behalf is the S8 shape this codebase already names in pairs.</para>
    /// </summary>
    Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>Projection for the device-revocation poll (ADR-0026): the three fields the directory keys on.</summary>
public sealed record DeactivatedDevice(string UserId, string DeviceId, DateTimeOffset DeactivatedOn);
