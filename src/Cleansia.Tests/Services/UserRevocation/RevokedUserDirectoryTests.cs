using Cleansia.Config.Services.UserRevocation;

namespace Cleansia.Tests.Services.UserRevocation;

/// <summary>
/// TC-REVOKE-USER-5: unit pins for the pure request-path guard (ADR-0027 D1). The iat rule IS the whole
/// enforcement contract: reset is a session kill, not a user ban.
/// </summary>
public class RevokedUserDirectoryTests
{
    private static readonly DateTimeOffset ResetAt = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static RevokedUserDirectory WithEntry(string userId, DateTimeOffset resetAt)
    {
        var directory = new RevokedUserDirectory();
        directory.Replace([new RevokedUserEntry(userId, resetAt)], resetAt);
        return directory;
    }

    [Fact]
    public void Token_issued_before_reset_is_revoked()
    {
        var directory = WithEntry("u1", ResetAt);

        Assert.True(directory.IsRevoked("u1", ResetAt.AddSeconds(-1)));
    }

    [Fact]
    public void Token_issued_after_reset_passes_even_while_entry_present()
    {
        var directory = WithEntry("u1", ResetAt);

        Assert.False(directory.IsRevoked("u1", ResetAt.AddSeconds(1)));
    }

    [Fact]
    public void Token_with_unreadable_iat_matching_an_entry_is_revoked()
    {
        var directory = WithEntry("u1", ResetAt);

        Assert.True(directory.IsRevoked("u1", tokenIssuedAt: null));
    }

    [Fact]
    public void Different_user_is_not_revoked()
    {
        var directory = WithEntry("u1", ResetAt);

        Assert.False(directory.IsRevoked("u2", ResetAt.AddSeconds(-1)));
    }

    [Fact]
    public void Empty_directory_revokes_nothing()
    {
        var directory = new RevokedUserDirectory();

        Assert.False(directory.IsRevoked("u1", null));
    }

    [Fact]
    public void Replace_keeps_the_latest_reset_instant_for_a_user()
    {
        var directory = new RevokedUserDirectory();
        var later = ResetAt.AddMinutes(10);

        directory.Replace(
            [
                new RevokedUserEntry("u1", ResetAt),
                new RevokedUserEntry("u1", later),
            ],
            later);

        // A session minted between the two resets must still be killed by the later one.
        Assert.True(directory.IsRevoked("u1", ResetAt.AddMinutes(5)));
    }

    [Fact]
    public void Same_wall_clock_second_relogin_is_revoked_once_then_a_later_second_passes()
    {
        // iat truncates to the whole second (12:00:00); resetAt is sub-second (12:00:00.200). The strict
        // < guard fails this re-login once (it self-heals via its live refresh token, ADR-0027 U1);
        // a token from the next whole second postdates the reset and passes.
        var subSecondReset = new DateTimeOffset(2026, 7, 15, 12, 0, 0, 200, TimeSpan.Zero);
        var directory = WithEntry("u1", subSecondReset);

        Assert.True(directory.IsRevoked("u1", new DateTimeOffset(2026, 7, 15, 12, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(directory.IsRevoked("u1", new DateTimeOffset(2026, 7, 15, 12, 0, 1, 0, TimeSpan.Zero)));
    }
}
