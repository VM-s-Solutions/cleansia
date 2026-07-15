using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
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
/// Service-level contract for the rotation-vs-revoke fix (X2/D9.7): the happy-path rotation still works
/// end-to-end (it now flushes its own unit of work so the concurrency collision can be caught), and a
/// rotation of a token that a revoke already killed fails closed — throws and leaves no live token, so
/// the revoke wins. Runs over SQLite for the service wiring; the deterministic xmin race (which SQLite
/// can't enforce) is proven against real Postgres in the integration suite.
/// </summary>
public sealed class RefreshTokenServiceRotationFailClosedTests : IDisposable
{
    private const string UserId = "user-rot";
    private const string Audience = JwtAudiences.Mobile;
    private const string DeviceId = "android-id-rot";

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceRotationFailClosedTests()
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
            new RefreshTokenRepository(ctx),
            ctx,
            jwt.Object,
            NullLogger<RefreshTokenService>.Instance);
    }

    private async Task<string> SeedActiveTokenAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Language.Create("en", "English"));
        var user = User.CreateWithPassword($"{UserId}@cleansia.test", "Passw0rd!", "Owner", "Rot");
        user.Id = UserId;
        ctx.Add(user);

        var raw = NewService(ctx).Issue(UserId, rememberMe: true, audience: Audience, deviceId: DeviceId).RawToken;
        await ctx.CommitAsync(CancellationToken.None);
        return raw;
    }

    [Fact]
    public async Task Rotation_Happy_Path_Persists_Exactly_One_Live_Child()
    {
        var raw = await SeedActiveTokenAsync();

        IssuedRefreshToken issued;
        await using (var ctx = NewContext())
        {
            var service = NewService(ctx);
            issued = await service.RotateAsync(raw, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: DeviceId);
            await service.CommitRotationAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var live = await new RefreshTokenRepository(assertCtx).GetActiveByUserIdAsync(UserId, CancellationToken.None);

        var single = Assert.Single(live);
        Assert.Equal(issued.Record.Id, single.Id);
        Assert.Equal(DeviceId, single.DeviceId);
    }

    [Fact]
    public async Task Rotation_Of_An_Already_Revoked_Token_Fails_Closed_With_No_Live_Token()
    {
        var raw = await SeedActiveTokenAsync();

        // The revoke wins first (models the race resolving in the revoke's favour before the rotation).
        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeByDeviceAsync(UserId, DeviceId, "device_revoked", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            await Assert.ThrowsAsync<RefreshTokenValidationException>(async () =>
                await NewService(ctx).RotateAsync(raw, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: DeviceId));
        }

        await using var assertCtx = NewContext();
        var live = await new RefreshTokenRepository(assertCtx).GetActiveByUserIdAsync(UserId, CancellationToken.None);

        Assert.Empty(live);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
