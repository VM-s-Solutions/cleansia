using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Devices;

public class Device : Auditable, ITenantEntity
{
    public string UserId { get; private set; } = default!;
    public string Platform { get; private set; } = default!;
    public string DeviceToken { get; private set; } = default!;
    public string DeviceId { get; private set; } = default!;
    public DateTimeOffset LastActiveAt { get; private set; }

    /// <summary>
    /// System-level kill switch driven by the OS notification permission. When
    /// false, the push dispatcher skips this row entirely — even if the user's
    /// per-category preferences allow the event. Set on register (user just
    /// granted permission) and updated when the app reports the OS revoke.
    /// </summary>
    public bool NotificationsEnabled { get; private set; } = true;

    public virtual User User { get; private set; } = default!;

    private Device() { }

    public static Device Create(string userId, string platform, string deviceToken, string deviceId)
    {
        return new Device
        {
            UserId = userId,
            Platform = platform,
            DeviceToken = deviceToken,
            DeviceId = deviceId,
            LastActiveAt = DateTimeOffset.UtcNow,
            NotificationsEnabled = true
        };
    }

    /// <summary>
    /// Re-registers this device for the owning user: refreshes the push token and
    /// last-active stamp and — critically — reactivates the row. Logout soft-deletes
    /// the device (IsActive=false) but leaves it physically present, and (UserId,
    /// DeviceId) is uniquely indexed across active AND inactive rows, so the next
    /// login must RECLAIM this tombstone rather than insert a colliding duplicate.
    /// Registration only runs after the OS grants notification permission, so
    /// notifications are (re)enabled here too — matching <see cref="Create"/>.
    /// </summary>
    public void MarkRegistered(string deviceToken)
    {
        DeviceToken = deviceToken;
        LastActiveAt = DateTimeOffset.UtcNow;

        // Reactivate + (re)enable ONLY on the reclaim transition (a logged-out
        // tombstone coming back). Reaching an already-active row is a routine
        // token refresh on launch/rotation — it must not clobber a user's
        // disabled flag once an OS-revoke-report path exists.
        if (!IsActive)
        {
            NotificationsEnabled = true;
            IsActive = true;
        }
    }

    public void UpdateLastActive()
    {
        LastActiveAt = DateTimeOffset.UtcNow;
    }

    public void UpdateNotificationsEnabled(bool enabled)
    {
        NotificationsEnabled = enabled;
        LastActiveAt = DateTimeOffset.UtcNow;
    }
}
