using System.Security.Cryptography;
using System.Text;
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
/// Spins a real <see cref="CleansiaDbContext"/> over SQLite in-memory (the
/// <see cref="RefreshTokenServiceRevokeByDeviceTests"/> harness) to prove the credential-rotation
/// kill switch (ADR-0024 D4.6): revoke-all ends every active session a user holds — including
/// device-less web sessions a per-device revoke can never match — spares exactly the session
/// identified by <c>exceptRawToken</c> (the password-CHANGE caller), never touches another user's
/// tokens, and never rewrites the forensic reason on an already-revoked row.
/// </summary>
public sealed class RefreshTokenServiceRevokeAllTests : IDisposable
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";
    private const string Audience = JwtAudiences.Customer;
    private const string RawTokenB = "raw-refresh-token-b";

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceRevokeAllTests()
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
            NullLogger<RefreshTokenService>.Instance);
    }

    private static RefreshToken NewToken(string id, string userId, string hash, string? deviceId)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: Audience,
            deviceLabel: "Chrome 120 - macOS",
            ipAddress: "10.0.0.1",
            deviceId: deviceId);
        token.Id = id;
        return token;
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task SeedAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Language.Create("en", "English"));

        var user = User.CreateWithPassword("user@cleansia.test", "Passw0rd!", "Owner", "User");
        user.Id = UserId;
        ctx.Add(user);

        var otherUser = User.CreateWithPassword("other@cleansia.test", "Passw0rd!", "Other", "User");
        otherUser.Id = OtherUserId;
        ctx.Add(otherUser);

        ctx.Add(NewToken("tok-A", UserId, "hash-a", "android-id-aaaa"));
        ctx.Add(NewToken("tok-B", UserId, Hash(RawTokenB), deviceId: null));
        ctx.Add(NewToken("tok-web", UserId, "hash-web", deviceId: null));
        ctx.Add(NewToken("tok-rotated", UserId, "hash-rotated", deviceId: null)
            .Revoke("rotated", DateTimeOffset.UtcNow.AddHours(-1), replacedByTokenId: "tok-web"));
        ctx.Add(NewToken("tok-other-user", OtherUserId, "hash-other", deviceId: null));

        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Revokes_Every_Active_Token_Including_DeviceLess_Web_Sessions_When_None_Is_Spared()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeAllForUserAsync(UserId, "password_reset", exceptRawToken: null, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var tokens = await assertCtx.Set<RefreshToken>()
            .Where(t => t.UserId == UserId && t.Id != "tok-rotated")
            .ToListAsync();

        Assert.Equal(3, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.Equal("password_reset", t.RevokedReason);
            Assert.NotNull(t.RevokedAt);
            Assert.False(t.IsAlive);
        });
    }

    [Fact]
    public async Task Spares_Exactly_The_Session_Identified_By_The_Excepted_Raw_Token()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeAllForUserAsync(UserId, "password_changed", RawTokenB, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var tokenA = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-A");
        var tokenB = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-B");
        var tokenWeb = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-web");

        Assert.Equal("password_changed", tokenA.RevokedReason);
        Assert.Equal("password_changed", tokenWeb.RevokedReason);
        Assert.True(tokenB.IsAlive);
        Assert.Null(tokenB.RevokedAt);
    }

    [Fact]
    public async Task Never_Touches_Another_Users_Tokens()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeAllForUserAsync(UserId, "password_reset", exceptRawToken: null, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var otherUsersToken = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-other-user");

        Assert.True(otherUsersToken.IsAlive);
        Assert.Null(otherUsersToken.RevokedAt);
    }

    [Fact]
    public async Task Preserves_The_Forensic_Reason_On_An_Already_Revoked_Token()
    {
        await SeedAsync();

        await using (var ctx = NewContext())
        {
            await NewService(ctx).RevokeAllForUserAsync(UserId, "password_reset", exceptRawToken: null, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var assertCtx = NewContext();
        var rotated = await assertCtx.Set<RefreshToken>().FirstAsync(t => t.Id == "tok-rotated");

        // The rotation chain is the theft-detection evidence — a revoke-all must not rewrite it.
        Assert.Equal("rotated", rotated.RevokedReason);
        Assert.Equal("tok-web", rotated.ReplacedByTokenId);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
