using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// TC-LOGOUT-CHAIN-1..7 (T-0428): logging out with a ROTATED refresh token walks the token's
/// successor chain and revokes the live tip (reason <c>logout_chain</c>), so a thief who rotated a
/// stolen token just before the victim's logout can't keep the live successor alive indefinitely.
/// The walk is SESSION-scoped (chains never cross devices), OWNERSHIP-gated (only the token's own
/// account), and RESPONSE-invariant (no probing oracle). Runs over SQLite for the service wiring.
/// </summary>
public sealed class RefreshTokenServiceLogoutChainTests : IDisposable
{
    private const string UserId = "user-logout-chain";
    private const string OtherUserId = "user-other";
    private const string Audience = JwtAudiences.Mobile;
    private const string DeviceA = "device-a";
    private const string DeviceB = "device-b";

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceLogoutChainTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(UserId, $"{UserId}@cleansia.test"),
            new NullTenantProvider());
    }

    private static RefreshTokenService NewService(CleansiaDbContext ctx)
    {
        var jwt = new Mock<IJwtSettings>();
        jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
        jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
        return new RefreshTokenService(
            new RefreshTokenRepository(ctx), ctx, jwt.Object, NullLogger<RefreshTokenService>.Instance,
            TimeProvider.System);
    }

    private async Task SeedUsersAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
        ctx.Add(Language.Create("en", "English"));
        foreach (var (id, first) in new[] { (UserId, "Owner"), (OtherUserId, "Other") })
        {
            var user = User.CreateWithPassword($"{id}@cleansia.test", "Passw0rd!", first, "User");
            user.Id = id;
            ctx.Add(user);
        }
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<string> IssueAsync(string userId, string deviceId)
    {
        await using var ctx = NewContext();
        var raw = NewService(ctx).Issue(userId, rememberMe: true, audience: Audience, deviceId: deviceId).RawToken;
        await ctx.CommitAsync(CancellationToken.None);
        return raw;
    }

    /// <summary>Rotates <paramref name="raw"/> once and returns the new child's raw token.</summary>
    private async Task<string> RotateAsync(string raw, string deviceId)
    {
        await using var ctx = NewContext();
        var service = NewService(ctx);
        var issued = await service.RotateAsync(raw, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: deviceId);
        await service.CommitRotationAsync(CancellationToken.None);
        return issued.RawToken;
    }

    private async Task LogoutAsync(string raw, string? callerUserId)
    {
        await using var ctx = NewContext();
        await NewService(ctx).RevokeAsync(raw, reason: "logout", CancellationToken.None, callerUserId);
    }

    private static string Hash(string raw) => new RefreshTokenService(
        Mock.Of<IRefreshTokenRepository>(), Mock.Of<Cleansia.Core.Domain.SeedWork.IUnitOfWork>(),
        Mock.Of<IJwtSettings>(), NullLogger<RefreshTokenService>.Instance, TimeProvider.System).HashToken(raw);

    private async Task<RefreshToken?> ByRawAsync(string raw)
    {
        await using var ctx = NewContext();
        var hash = Hash(raw);
        return await ctx.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    private async Task<IReadOnlyList<RefreshToken>> LiveAsync(string userId)
    {
        await using var ctx = NewContext();
        return await new RefreshTokenRepository(ctx).GetActiveByUserIdAsync(userId, CancellationToken.None);
    }

    // ── TC-LOGOUT-CHAIN-1 — the headline ──

    [Fact]
    public async Task Owner_Logout_With_A_Rotated_Token_Revokes_The_Live_Successor()
    {
        await SeedUsersAsync();
        var rawA = await IssueAsync(UserId, DeviceA);
        var rawB = await RotateAsync(rawA, DeviceA);

        await LogoutAsync(rawA, UserId);

        var childB = await ByRawAsync(rawB);
        Assert.NotNull(childB);
        Assert.NotNull(childB!.RevokedAt);
        Assert.Equal("logout_chain", childB.RevokedReason);
        Assert.Empty(await LiveAsync(UserId));
    }

    // ── TC-LOGOUT-CHAIN-2 — multi-hop, forensic marks preserved ──

    [Fact]
    public async Task Multi_Hop_Chain_Revokes_The_Tip_And_Leaves_The_Rotated_Marks_Intact()
    {
        await SeedUsersAsync();
        var rawA = await IssueAsync(UserId, DeviceA);
        var rawB = await RotateAsync(rawA, DeviceA);
        var rawC = await RotateAsync(rawB, DeviceA);

        await LogoutAsync(rawA, UserId);

        // Intermediate B keeps its forensic "rotated" mark (reuse detection must still fire on it).
        Assert.Equal("rotated", (await ByRawAsync(rawB))!.RevokedReason);
        // The live tip C is the only one this walk newly revokes.
        var tipC = await ByRawAsync(rawC);
        Assert.Equal("logout_chain", tipC!.RevokedReason);
        Assert.Empty(await LiveAsync(UserId));
    }

    // ── TC-LOGOUT-CHAIN-3 — ownership gate ──

    [Fact]
    public async Task A_Different_User_Cannot_Trigger_The_Walk()
    {
        await SeedUsersAsync();
        var rawA = await IssueAsync(UserId, DeviceA);
        var rawB = await RotateAsync(rawA, DeviceA);

        // User B presents user A's rotated token. Silent no-op — A's successor stays alive.
        await LogoutAsync(rawA, OtherUserId);

        Assert.Null((await ByRawAsync(rawB))!.RevokedAt);
        Assert.Single(await LiveAsync(UserId));
    }

    [Fact]
    public async Task An_Unresolvable_Caller_Cannot_Trigger_The_Walk()
    {
        await SeedUsersAsync();
        var rawA = await IssueAsync(UserId, DeviceA);
        var rawB = await RotateAsync(rawA, DeviceA);

        await LogoutAsync(rawA, callerUserId: null);

        Assert.Null((await ByRawAsync(rawB))!.RevokedAt);
        Assert.Single(await LiveAsync(UserId));
    }

    // ── TC-LOGOUT-CHAIN-4 — anti-probing idempotency (only "rotated" walks) ──

    [Fact]
    public async Task Unknown_And_NonRotated_Tokens_Never_Walk_And_Stay_Idempotent()
    {
        await SeedUsersAsync();

        // Unknown token — no throw, no effect.
        await LogoutAsync("totally-unknown-raw", UserId);

        // A plain live token logged out the normal way is revoked "logout", not walked.
        var rawLive = await IssueAsync(UserId, DeviceA);
        await LogoutAsync(rawLive, UserId);
        Assert.Equal("logout", (await ByRawAsync(rawLive))!.RevokedReason);

        // A token already revoked for another reason is left untouched (no walk).
        var rawReset = await IssueAsync(UserId, DeviceB);
        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeAllForUserAsync(UserId, "password_reset", exceptRawToken: null, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }
        await LogoutAsync(rawReset, UserId);
        Assert.Equal("password_reset", (await ByRawAsync(rawReset))!.RevokedReason);

        // Re-presenting the same rotated parent twice is idempotent (tip already dead).
        var rawA = await IssueAsync(UserId, "device-idem");
        var rawB = await RotateAsync(rawA, "device-idem");
        await LogoutAsync(rawA, UserId);
        await LogoutAsync(rawA, UserId);
        Assert.Equal("logout_chain", (await ByRawAsync(rawB))!.RevokedReason);
    }

    // ── TC-LOGOUT-CHAIN-5 — sibling session isolation ──

    [Fact]
    public async Task The_Users_Other_Device_Chain_Survives()
    {
        await SeedUsersAsync();
        var rawA = await IssueAsync(UserId, DeviceA);
        await RotateAsync(rawA, DeviceA);

        // A separate live session on another device.
        var rawOther = await IssueAsync(UserId, DeviceB);

        await LogoutAsync(rawA, UserId);

        // Exactly the other device's token remains alive — the walk never went account-wide.
        var live = Assert.Single(await LiveAsync(UserId));
        Assert.Equal(DeviceB, live.DeviceId);
        Assert.Equal(Hash(rawOther), live.TokenHash);
    }

    // ── TC-LOGOUT-CHAIN-7 — the theft headline ──

    [Fact]
    public async Task Thief_Rotation_Before_Victim_Logout_Ends_With_The_Stolen_Successor_Dead()
    {
        await SeedUsersAsync();
        // Victim's session token H is stolen.
        var stolenH = await IssueAsync(UserId, DeviceA);
        // Thief rotates it → H′ (the thief now holds a live successor).
        var thiefHPrime = await RotateAsync(stolenH, DeviceA);

        // Victim logs out with the stale H they still hold.
        await LogoutAsync(stolenH, UserId);

        // The thief's H′ is dead — and revoked as logout_chain, NOT rotated, so a later
        // re-presentation does not spuriously fire the account-wide rotation-reuse revoke.
        var hPrime = await ByRawAsync(thiefHPrime);
        Assert.NotNull(hPrime!.RevokedAt);
        Assert.Equal("logout_chain", hPrime.RevokedReason);
        Assert.Empty(await LiveAsync(UserId));
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
