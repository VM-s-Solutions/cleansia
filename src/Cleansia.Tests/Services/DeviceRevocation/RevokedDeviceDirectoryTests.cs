using Cleansia.Config.Services.DeviceRevocation;

namespace Cleansia.Tests.Services.DeviceRevocation;

/// <summary>
/// Unit pins for the pure request-path guard (ADR-0026 D2). The iat rule IS the whole enforcement
/// contract: revoke is a session kill, not a device ban.
/// </summary>
public class RevokedDeviceDirectoryTests
{
    private static readonly DateTimeOffset RevokedAt = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static RevokedDeviceDirectory WithEntry(string userId, string deviceId, DateTimeOffset revokedAt)
    {
        var directory = new RevokedDeviceDirectory();
        directory.Replace([new RevokedDeviceEntry(userId, deviceId, revokedAt)], revokedAt);
        return directory;
    }

    [Fact]
    public void Token_issued_before_revocation_is_revoked()
    {
        var directory = WithEntry("u1", "A", RevokedAt);

        Assert.True(directory.IsRevoked("u1", "A", RevokedAt.AddSeconds(-1)));
    }

    [Fact]
    public void Token_issued_after_revocation_passes_even_while_entry_present()
    {
        var directory = WithEntry("u1", "A", RevokedAt);

        Assert.False(directory.IsRevoked("u1", "A", RevokedAt.AddSeconds(1)));
    }

    [Fact]
    public void Token_with_unreadable_iat_matching_an_entry_is_revoked()
    {
        var directory = WithEntry("u1", "A", RevokedAt);

        Assert.True(directory.IsRevoked("u1", "A", tokenIssuedAt: null));
    }

    [Fact]
    public void Sibling_device_of_same_user_is_not_revoked()
    {
        var directory = WithEntry("u1", "A", RevokedAt);

        Assert.False(directory.IsRevoked("u1", "B", RevokedAt.AddSeconds(-1)));
    }

    [Fact]
    public void Different_user_same_device_id_is_not_revoked()
    {
        var directory = WithEntry("u1", "A", RevokedAt);

        Assert.False(directory.IsRevoked("u2", "A", RevokedAt.AddSeconds(-1)));
    }

    [Fact]
    public void Empty_directory_revokes_nothing()
    {
        var directory = new RevokedDeviceDirectory();

        Assert.False(directory.IsRevoked("u1", "A", null));
    }

    [Fact]
    public void Replace_keeps_the_latest_revocation_instant_for_a_key()
    {
        var directory = new RevokedDeviceDirectory();
        var later = RevokedAt.AddMinutes(10);

        directory.Replace(
            [
                new RevokedDeviceEntry("u1", "A", RevokedAt),
                new RevokedDeviceEntry("u1", "A", later),
            ],
            later);

        // A session minted between the two revocations must still be killed by the later one.
        Assert.True(directory.IsRevoked("u1", "A", RevokedAt.AddMinutes(5)));
    }
}
