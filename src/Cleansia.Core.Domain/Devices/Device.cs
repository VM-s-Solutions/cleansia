using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Devices;

public class Device : Auditable
{
    public string UserId { get; private set; } = default!;
    public string Platform { get; private set; } = default!;
    public string DeviceToken { get; private set; } = default!;
    public string DeviceId { get; private set; } = default!;
    public DateTimeOffset LastActiveAt { get; private set; }

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
            LastActiveAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateToken(string deviceToken)
    {
        DeviceToken = deviceToken;
        LastActiveAt = DateTimeOffset.UtcNow;
    }

    public void UpdateLastActive()
    {
        LastActiveAt = DateTimeOffset.UtcNow;
    }
}
