using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// The multi-tenant token-revoke symmetry against a REAL Postgres DbContext (Testcontainers).
/// The EF global tenant query filter and IgnoreQueryFilters() translate to SQL the production way here —
/// the SQLite unit test proves the shape, this proves it on the production provider.
///
/// Scenario: refresh tokens are issued on the ANONYMOUS login/refresh path (no tenant claim) so their
/// rows are stamped TenantId == null; the revoke read runs on an AUTHENTICATED request carrying a
/// non-null tenant_id. The repository reads must clear the filter and re-scope to the caller's own
/// UserId / token hash, so the revoke updates the row (affected-rows &gt; 0 — the token is actually dead)
/// and never reaches another user's token.
/// </summary>
[Collection("PostgresCollection")]
public class RefreshTokenTenantRevokePostgresTests : BaseIntegrationTest
{
    private const string TenantA = "tenant-A";
    private const string UserA = "user-A";
    private const string UserB = "user-B";
    private const string Audience = JwtAudiences.Mobile;
    private const string DeviceA = "android-id-aaaa";

    public RefreshTokenTenantRevokePostgresTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private CleansiaDbContext NewContext(string? tenantId, string userId = UserA)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
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
            NullLogger<RefreshTokenService>.Instance,
            TimeProvider.System);
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

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
    }

    private async Task SeedAsync()
    {
        await using var ctx = NewContext(tenantId: null);

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
    }

    [Fact]
    public async Task RevokeByDeviceAsync_FromTenantContext_ActuallyRevokesNullStampedToken()
    {
        await ResetAsync();
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
        await ResetAsync();
        await SeedAsync();

        string rawA;
        await using (var issueCtx = NewContext(tenantId: null))
        {
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
    public async Task RevokeByDeviceAsync_FromTenantContext_NeverRevokesAnotherUsersToken()
    {
        await ResetAsync();
        await SeedAsync();

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
