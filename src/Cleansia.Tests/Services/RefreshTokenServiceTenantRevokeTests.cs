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
/// Proves the multi-tenant token-revoke symmetry. Refresh tokens are issued on the
/// ANONYMOUS login/refresh path (no tenant claim) so their rows are stamped <c>TenantId == null</c>.
/// The revoke-side reads (logout, per-device kill, rotation-reuse chain revoke) run on an
/// AUTHENTICATED request whose JWT carries a non-null <c>tenant_id</c>. Under the EF global tenant
/// filter a tenant-claimed read would hide the user's own null-stamped rows, so the revoke would
/// silently match zero rows and a logged-out / compromised token would keep working.
///
/// Spins a REAL <see cref="CleansiaDbContext"/> over SQLite in-memory so the global tenant query
/// filter actually runs. The repository reads must clear the filter and re-scope to the caller's own
/// UserId / token hash, so the revoke updates the row even when the request carries a tenant id.
/// </summary>
public sealed class RefreshTokenServiceTenantRevokeTests : IDisposable
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";
    private const string UserA = "user-A";
    private const string UserB = "user-B";
    private const string Audience = JwtAudiences.Mobile;
    private const string DeviceA = "android-id-aaaa";

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceTenantRevokeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId, string userId = UserA)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(userId, $"{userId}@cleansia.test"),
            new FixedTenantProvider(tenantId));
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
            NullLogger<RefreshTokenService>.Instance);
    }

    private static RefreshToken NewToken(string id, string userId, string hash, string? deviceId)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: Audience,
            deviceLabel: "Pixel 9 Pro - Android 15",
            ipAddress: "10.0.0.1",
            deviceId: deviceId);
        token.Id = id;
        return token;
    }

    /// <summary>
    /// Seeds two users (one per tenant) each holding a refresh token whose row is stamped
    /// <c>TenantId == null</c> — exactly what the anonymous issuance path produces today.
    /// </summary>
    private async Task SeedAsync()
    {
        await using var ctx = NewContext(tenantId: null);
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Language.Create("en", "English"));

        var userA = User.CreateWithPassword(UserA + "@cleansia.test", "Passw0rd!", "Owner", "A");
        userA.Id = UserA;
        var userB = User.CreateWithPassword(UserB + "@cleansia.test", "Passw0rd!", "Owner", "B");
        userB.Id = UserB;
        ctx.Add(userA);
        ctx.Add(userB);

        ctx.Add(NewToken("tok-A", UserA, "hash-a", DeviceA));
        ctx.Add(NewToken("tok-B", UserB, "hash-b", deviceId: "android-id-bbbb"));

        await ctx.CommitAsync(CancellationToken.None);

        await using var verify = NewContext(tenantId: null);
        var seeded = await verify.Set<RefreshToken>().IgnoreQueryFilters().FirstAsync(t => t.Id == "tok-A");
        Assert.Null(seeded.TenantId);
    }

    [Fact]
    public async Task RevokeByDeviceAsync_FromTenantContext_ActuallyRevokesNullStampedToken()
    {
        await SeedAsync();

        await using (var ctx = NewContext(tenantId: TenantA))
        {
            await NewService(ctx).RevokeByDeviceAsync(UserA, DeviceA, "device_revoked", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext(tenantId: null);
        var tokenA = await assertCtx.Set<RefreshToken>().IgnoreQueryFilters().FirstAsync(t => t.Id == "tok-A");

        Assert.NotNull(tokenA.RevokedAt);
        Assert.False(tokenA.IsAlive);
        Assert.Equal("device_revoked", tokenA.RevokedReason);
    }

    [Fact]
    public async Task RevokeAsync_Logout_FromTenantContext_ActuallyRevokesNullStampedToken()
    {
        await SeedAsync();

        // Issue a fresh token on the anonymous path (TenantId stamped null), keeping its raw value.
        string rawA;
        await using (var issueCtx = NewContext(tenantId: null))
        {
            await issueCtx.Database.EnsureCreatedAsync();
            rawA = NewService(issueCtx).Issue(UserA, rememberMe: true, audience: Audience, deviceId: DeviceA).RawToken;
            await issueCtx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext(tenantId: TenantA))
        {
            await NewService(ctx).RevokeAsync(rawA, "logout", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext(tenantId: null);
        var hash = NewService(assertCtx).HashToken(rawA);
        var issued = await assertCtx.Set<RefreshToken>().IgnoreQueryFilters().FirstAsync(t => t.TokenHash == hash);

        Assert.NotNull(issued.RevokedAt);
        Assert.False(issued.IsAlive);
        Assert.Equal("logout", issued.RevokedReason);
    }

    [Fact]
    public async Task RevokeByDeviceAsync_NeverRevokesAnotherUsersToken()
    {
        await SeedAsync();

        // User A, authenticated under tenant A, asks to revoke device A. User B's token (different
        // user, would be tenant B once stamped) must stay alive — the read is user-scoped, not widened.
        await using (var ctx = NewContext(tenantId: TenantA))
        {
            await NewService(ctx).RevokeByDeviceAsync(UserA, DeviceA, "device_revoked", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext(tenantId: null);
        var tokenB = await assertCtx.Set<RefreshToken>().IgnoreQueryFilters().FirstAsync(t => t.Id == "tok-B");

        Assert.True(tokenB.IsAlive);
        Assert.Null(tokenB.RevokedAt);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
