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
/// Spins a real <see cref="CleansiaDbContext"/> over SQLite in-memory so the device-revoke
/// match runs against the actual EF pipeline and the real RefreshToken entity — not a mock of
/// <see cref="IRefreshTokenService"/>. This is the proof that a per-device revoke matches on the
/// stable <see cref="RefreshToken.DeviceId"/> (the value the app registers) and not the
/// human-readable DeviceLabel: it kills exactly the target device's token, leaves a sibling
/// device's token alive, and never matches a token whose DeviceId is null.
/// </summary>
public sealed class RefreshTokenServiceRevokeByDeviceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string Audience = JwtAudiences.Mobile;
    private const string DeviceA = "android-id-aaaa";
    private const string DeviceB = "android-id-bbbb";

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceRevokeByDeviceTests()
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
            new TestUserSessionProvider(UserId, "user@cleansia.test"),
            new NullTenantProvider());
    }

    private static RefreshTokenService NewService(CleansiaDbContext ctx)
    {
        var jwt = new Mock<IJwtSettings>();
        jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
        jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
        return new RefreshTokenService(
            new RefreshTokenRepository(ctx),
            ctx,
            jwt.Object,
            NullLogger<RefreshTokenService>.Instance,
            TimeProvider.System);
    }

    private static RefreshToken NewToken(string id, string hash, string? deviceId)
    {
        var token = RefreshToken.Create(
            userId: UserId,
            tokenHash: hash,
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: Audience,
            deviceLabel: "Pixel 9 Pro - Android 15",
            ipAddress: "10.0.0.1",
            deviceId: deviceId);
        token.Id = id;
        return token;
    }

    private async Task SeedAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Language.Create("en", "English"));

        var user = User.CreateWithPassword("user@cleansia.test", "Passw0rd!", "Owner", "User");
        user.Id = UserId;
        ctx.Add(user);

        ctx.Add(NewToken("tok-A", "hash-a", DeviceA));
        ctx.Add(NewToken("tok-B", "hash-b", DeviceB));
        ctx.Add(NewToken("tok-null", "hash-null", deviceId: null));

        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RevokeByDeviceAsync_Revokes_Only_The_Target_Devices_Token()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeByDeviceAsync(UserId, DeviceA, "device_revoked", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var tokenA = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-A");
        var tokenB = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-B");

        Assert.NotNull(tokenA.RevokedAt);
        Assert.Equal("device_revoked", tokenA.RevokedReason);
        Assert.False(tokenA.IsAlive);
        Assert.True(tokenB.IsAlive);
    }

    [Fact]
    public async Task RevokeByDeviceAsync_Leaves_A_Null_DeviceId_Token_Alive()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeByDeviceAsync(UserId, DeviceA, "device_revoked", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var tokenNull = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-null");

        // A null DeviceId must never match a device revoke — proves the null-guard.
        Assert.True(tokenNull.IsAlive);
        Assert.Null(tokenNull.RevokedAt);
    }

    [Fact]
    public async Task RevokeByDeviceAsync_Matching_The_Other_Device_Leaves_The_First_Alive()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeByDeviceAsync(UserId, DeviceB, "device_revoked", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var tokenA = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-A");
        var tokenB = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-B");

        Assert.True(tokenA.IsAlive);
        Assert.NotNull(tokenB.RevokedAt);
        Assert.False(tokenB.IsAlive);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
