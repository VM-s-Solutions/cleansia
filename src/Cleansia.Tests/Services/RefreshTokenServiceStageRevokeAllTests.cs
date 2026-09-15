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
/// The erasure's revoke (owner ruling 2026-09-14). <c>RevokeAllForUserAsync</c> commits the unit of
/// work itself — the right shape for a password reset, where the kill switch must land even if the
/// caller's own commit is outraced — but inside <c>GdprDeletionService</c> that commit split the erasure
/// in two: everything staged before it was durable, everything after it could still roll back. So the
/// erasure gets a revoke that only STAGES: the tokens die in the caller's context and nowhere else until
/// the caller's single commit carries them, and a concurrency collision on a token row is then the
/// erasure's own commit failure rather than a retried side path.
/// </summary>
public sealed class RefreshTokenServiceStageRevokeAllTests : IDisposable
{
    private const string UserId = "user-stage-1";
    private const string OtherUserId = "user-stage-2";
    private const string Reason = "gdpr_erasure";

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceStageRevokeAllTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Staging_Revokes_Nothing_Durably_Until_The_Caller_Commits()
    {
        await SeedAsync();

        await using var ctx = NewContext();
        await NewService(ctx).StageRevokeAllForUserAsync(UserId, Reason, CancellationToken.None);

        await using (var before = NewContext())
        {
            var alive = await before.Set<RefreshToken>().Where(t => t.UserId == UserId && t.RevokedAt == null).CountAsync();
            Assert.Equal(3, alive);
        }

        await ctx.CommitAsync(CancellationToken.None);

        await using var after = NewContext();
        var tokens = await after.Set<RefreshToken>().Where(t => t.UserId == UserId && t.Id != "tok-rotated").ToListAsync();
        Assert.Equal(3, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.Equal(Reason, t.RevokedReason);
            Assert.NotNull(t.RevokedAt);
            Assert.False(t.IsAlive);
        });
    }

    [Fact]
    public async Task Staging_Leaves_Another_Users_Tokens_And_An_Already_Revoked_Row_Alone()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).StageRevokeAllForUserAsync(UserId, Reason, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var other = await assertCtx.Set<RefreshToken>().SingleAsync(t => t.Id == "tok-other-user");
        Assert.Null(other.RevokedAt);
        Assert.True(other.IsAlive);

        var rotated = await assertCtx.Set<RefreshToken>().SingleAsync(t => t.Id == "tok-rotated");
        Assert.Equal("rotated", rotated.RevokedReason);
        Assert.Equal("tok-web", rotated.ReplacedByTokenId);
    }

    [Fact]
    public async Task Staging_Never_Commits_Even_When_Nothing_Matches()
    {
        await SeedAsync();
        var unitOfWork = new Mock<Core.Domain.SeedWork.IUnitOfWork>(MockBehavior.Strict);

        await using var ctx = NewContext();
        var service = new RefreshTokenService(
            new RefreshTokenRepository(ctx), unitOfWork.Object, JwtSettings(), NullLogger<RefreshTokenService>.Instance, TimeProvider.System);

        await service.StageRevokeAllForUserAsync("user-with-no-tokens", Reason, CancellationToken.None);
        await service.StageRevokeAllForUserAsync(UserId, Reason, CancellationToken.None);

        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(UserId, "stage@cleansia.test"),
            new DefaultTenantProvider());
    }

    private static IJwtSettings JwtSettings()
    {
        var jwt = new Mock<IJwtSettings>();
        jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
        jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
        return jwt.Object;
    }

    private static RefreshTokenService NewService(CleansiaDbContext ctx) =>
        new(new RefreshTokenRepository(ctx), ctx, JwtSettings(), NullLogger<RefreshTokenService>.Instance, TimeProvider.System);

    private static RefreshToken NewToken(string id, string userId, string hash, string? deviceId)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: JwtAudiences.Customer,
            deviceLabel: "Chrome 120 - macOS",
            ipAddress: "10.0.0.1",
            deviceId: deviceId);
        token.Id = id;
        return token;
    }

    private async Task SeedAsync()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);

        ctx.Add(Language.Create("en", "English"));

        var user = User.CreateWithPassword("stage@cleansia.test", "Passw0rd!", "Owner", "User");
        user.Id = UserId;
        ctx.Add(user);

        var otherUser = User.CreateWithPassword("other-stage@cleansia.test", "Passw0rd!", "Other", "User");
        otherUser.Id = OtherUserId;
        ctx.Add(otherUser);

        ctx.Add(NewToken("tok-A", UserId, "hash-a", "android-id-aaaa"));
        ctx.Add(NewToken("tok-B", UserId, "hash-b", deviceId: null));
        ctx.Add(NewToken("tok-web", UserId, "hash-web", deviceId: null));
        ctx.Add(NewToken("tok-rotated", UserId, "hash-rotated", deviceId: null)
            .Revoke("rotated", DateTimeOffset.UtcNow.AddHours(-1), replacedByTokenId: "tok-web"));
        ctx.Add(NewToken("tok-other-user", OtherUserId, "hash-other", deviceId: null));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
