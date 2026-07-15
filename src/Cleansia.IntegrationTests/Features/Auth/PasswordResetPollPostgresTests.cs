using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// TC-REVOKE-USER-9 (ADR-0027 U4) — the RevokedUserDirectory poll source
/// <see cref="IRefreshTokenRepository.GetPasswordResetsSinceAsync"/> against a REAL Postgres DbContext
/// (Testcontainers). The projection is a <c>GroupBy(UserId).Select(g =&gt; new UserPasswordReset(g.Key,
/// g.Max(t =&gt; t.RevokedAt!.Value)))</c> aggregate over a NULLABLE column — exactly the class of idiom
/// that has passed an in-memory fake and 500'd on Npgsql in this codebase (the standing group-by/
/// aggregate/null-projection lesson). This proves it translates server-side and returns
/// <c>(UserId, MAX(RevokedAt))</c> for the in-horizon <c>password_reset</c> rows while omitting
/// <c>password_changed</c> / <c>logout</c> / out-of-horizon rows.
/// </summary>
[Collection("PostgresCollection")]
public class PasswordResetPollPostgresTests : BaseIntegrationTest
{
    private const string UserA = "user-reset-A";
    private const string UserB = "user-reset-B";
    private const string UserC = "user-change-C";
    private const string Audience = JwtAudiences.Mobile;

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    // A's earlier reset, A's later reset (a double-reset inside the horizon), B's single reset.
    private static readonly DateTimeOffset AResetEarly = Now.AddMinutes(-20);
    private static readonly DateTimeOffset AResetLate = Now.AddMinutes(-3);
    private static readonly DateTimeOffset BReset = Now.AddMinutes(-10);

    // Negatives: out-of-horizon reset, a password_changed row, a logout row.
    private static readonly DateTimeOffset AResetOutOfHorizon = Now.AddMinutes(-90);

    public PasswordResetPollPostgresTests(PostgresContainerFixture fixture) : base(fixture)
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

    private static RefreshToken RevokedToken(string id, string userId, string hash, string reason, DateTimeOffset revokedAt)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: revokedAt.AddDays(7),
            audience: Audience,
            deviceLabel: null,
            ipAddress: null);
        token.Id = id;
        token.Revoke(reason, revokedAt);
        return token;
    }

    private static RefreshToken LiveToken(string id, string userId, string hash)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: Now.AddDays(7),
            audience: Audience,
            deviceLabel: null,
            ipAddress: null);
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
        var userC = User.CreateWithPassword(UserC + "@cleansia.test", "Passw0rd!", "Owner", "C");
        userC.Id = UserC;
        ctx.Add(userA);
        ctx.Add(userB);
        ctx.Add(userC);

        // A: two in-horizon password_reset rows (double reset) + one out-of-horizon reset.
        ctx.Add(RevokedToken("a-reset-early", UserA, "hash-a-early", "password_reset", AResetEarly));
        ctx.Add(RevokedToken("a-reset-late", UserA, "hash-a-late", "password_reset", AResetLate));
        ctx.Add(RevokedToken("a-reset-old", UserA, "hash-a-old", "password_reset", AResetOutOfHorizon));

        // B: one in-horizon password_reset row.
        ctx.Add(RevokedToken("b-reset", UserB, "hash-b", "password_reset", BReset));

        // Negatives that must be omitted: a password_changed row (in horizon), a logout row (in horizon),
        // and a live (never-revoked) token.
        ctx.Add(RevokedToken("c-changed", UserC, "hash-c-changed", "password_changed", Now.AddMinutes(-5)));
        ctx.Add(RevokedToken("b-logout", UserB, "hash-b-logout", "logout", Now.AddMinutes(-4)));
        ctx.Add(LiveToken("a-live", UserA, "hash-a-live"));

        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GetPasswordResetsSinceAsync_Returns_Max_ResetAt_Per_User_And_Omits_NonReset_And_OutOfHorizon()
    {
        await ResetAsync();
        await SeedAsync();

        var cutoff = Now.AddMinutes(-35);

        await using var ctx = NewContext(tenantId: "some-tenant");
        var repo = new RefreshTokenRepository(ctx);

        var result = await repo.GetPasswordResetsSinceAsync(cutoff, CancellationToken.None);

        // Exactly A and B (C's only row is password_changed; B's logout row is not a reset).
        Assert.Equal(2, result.Count);

        var a = Assert.Single(result, r => r.UserId == UserA);
        var b = Assert.Single(result, r => r.UserId == UserB);

        // A's most-recent in-horizon reset wins over the earlier one; the out-of-horizon row is excluded.
        Assert.Equal(AResetLate, a.ResetAt);
        Assert.Equal(BReset, b.ResetAt);

        // C (password_changed only) is never in the result — the D3 feed predicate is password_reset alone.
        Assert.DoesNotContain(result, r => r.UserId == UserC);
    }

    [Fact]
    public async Task GetPasswordResetsSinceAsync_Excludes_Everything_When_Cutoff_Is_After_All_Resets()
    {
        await ResetAsync();
        await SeedAsync();

        // A cutoff newer than every reset instant leaves nothing in the horizon.
        var cutoff = Now.AddMinutes(1);

        await using var ctx = NewContext(tenantId: null);
        var repo = new RefreshTokenRepository(ctx);

        var result = await repo.GetPasswordResetsSinceAsync(cutoff, CancellationToken.None);

        Assert.Empty(result);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
