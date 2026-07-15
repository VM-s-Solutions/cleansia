using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
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
/// The rotation-vs-revoke escape (X2/D9.7): a rotation reads its parent token as still-active, a
/// concurrent revoke commits between that read and the rotation's save, and the rotation then persists a
/// brand-new child token whose iat postdates the revoke — so it escapes the revocation chain and passes
/// the device directory, invisible in Devices.
///
/// The fix is an xmin optimistic-concurrency token on the parent RefreshToken row (a Postgres system
/// column — no migration). Both the revoke and the rotation write that same parent row, so whichever
/// commits second collides: the rotation's flush throws DbUpdateConcurrencyException, the whole rotation
/// rolls back (parent mark AND the new child insert), and the revoke wins. This proves the escaped token
/// never survives, run against REAL Postgres where xmin actually enforces (SQLite has no system row
/// version so it can't prove this — hence Testcontainers).
/// </summary>
[Collection("PostgresCollection")]
public class RefreshTokenRotationRevokeRaceTests : BaseIntegrationTest
{
    private const string UserId = "user-race";
    private const string Audience = JwtAudiences.Mobile;
    private const string DeviceId = "android-id-race";

    public RefreshTokenRotationRevokeRaceTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
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

    private async Task<string> SeedActiveTokenAsync()
    {
        await using var ctx = NewContext();

        ctx.Add(Language.Create("en", "English"));

        var user = User.CreateWithPassword($"{UserId}@cleansia.test", "Passw0rd!", "Owner", "Race");
        user.Id = UserId;
        ctx.Add(user);

        var raw = NewService(ctx).Issue(UserId, rememberMe: true, audience: Audience, deviceId: DeviceId).RawToken;
        await ctx.CommitAsync(CancellationToken.None);
        return raw;
    }

    private async Task<List<RefreshToken>> LiveTokensAsync()
    {
        await using var assertCtx = NewContext();
        var now = DateTimeOffset.UtcNow;
        return await assertCtx.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(t => t.UserId == UserId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync();
    }

    // Stages an in-flight rotation on `rotateCtx`: mirrors RotateAsync by inserting a child + marking the
    // parent rotated, tracked but NOT yet committed, so a concurrent revoke can commit against the parent
    // row (bumping its xmin) before this rotation flushes.
    private RefreshToken StagePendingRotation(CleansiaDbContext rotateCtx, string parentRawToken)
    {
        var hash = NewService(rotateCtx).HashToken(parentRawToken);
        var parent = rotateCtx.Set<RefreshToken>().IgnoreQueryFilters().First(t => t.TokenHash == hash);
        var child = RefreshToken.Create(
            userId: UserId,
            tokenHash: NewService(rotateCtx).HashToken(parentRawToken + "-rotated"),
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            audience: Audience,
            deviceLabel: null,
            ipAddress: null,
            deviceId: DeviceId);
        rotateCtx.Add(child);
        parent.MarkUsed(DateTimeOffset.UtcNow);
        parent.Revoke("rotated", DateTimeOffset.UtcNow, replacedByTokenId: child.Id);
        return parent;
    }

    [Fact]
    public async Task Rotation_Whose_Parent_Xmin_Changed_After_Its_Read_Throws_And_Persists_No_Child()
    {
        await ResetAsync();
        var raw = await SeedActiveTokenAsync();

        // Deterministically hit the concurrency collision: the rotation loads + mutates its parent while
        // it is still active, THEN a concurrent revoke commits against that same parent row on another
        // context (bumping xmin), and only then does the rotation flush. The rotation's save must throw
        // and leave no escaped child token. This is the raw-xmin guard (neutering IsConcurrencyToken
        // makes it go red).
        await using var rotateCtx = NewContext();
        StagePendingRotation(rotateCtx, raw);

        await using (var revokeCtx = NewContext())
        {
            await NewService(revokeCtx).RevokeByDeviceAsync(UserId, DeviceId, "device_revoked", CancellationToken.None);
            await revokeCtx.CommitAsync(CancellationToken.None);
        }

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await rotateCtx.CommitAsync(CancellationToken.None));

        // The child never persisted; the parent is dead — zero live tokens escaped the revoke.
        Assert.Empty(await LiveTokensAsync());
    }

    // Gate 4d: the revoke paths must SUCCEED (not 500) when a rotation races them, and leave zero live
    // tokens. The revoke commits FIRST against the parent row (bumping xmin); the racing rotation then
    // fails closed on its own flush. The revoke itself never throws.

    [Fact]
    public async Task RevokeAllForUser_Racing_A_Rotation_Succeeds_And_Leaves_No_Live_Token()
    {
        await ResetAsync();
        var raw = await SeedActiveTokenAsync();

        await using var rotateCtx = NewContext();
        StagePendingRotation(rotateCtx, raw);

        // The account-takeover kill switch runs concurrently. It must not throw (no 500) even though a
        // rotation is mid-flight on the same row.
        await using (var revokeCtx = NewContext())
        {
            await NewService(revokeCtx).RevokeAllForUserAsync(UserId, "password_reset", exceptRawToken: null, CancellationToken.None);
        }

        // The racing rotation, committing after the revoke bumped xmin, fails closed on its own flush.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await rotateCtx.CommitAsync(CancellationToken.None));

        Assert.Empty(await LiveTokensAsync());
    }

    [Fact]
    public async Task Logout_Revoke_Racing_A_Rotation_Succeeds_And_Leaves_No_Live_Token()
    {
        await ResetAsync();
        var raw = await SeedActiveTokenAsync();

        await using var rotateCtx = NewContext();
        StagePendingRotation(rotateCtx, raw);

        // Logout revoke of the same token, concurrent with the rotation — must succeed, not 500.
        await using (var revokeCtx = NewContext())
        {
            await NewService(revokeCtx).RevokeAsync(raw, "logout", CancellationToken.None);
        }

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await rotateCtx.CommitAsync(CancellationToken.None));

        Assert.Empty(await LiveTokensAsync());
    }

    // The regression the reviewer caught: a revoke whose OWN commit hits the xmin conflict must NOT 500 —
    // it retries to success (revocation is idempotent). We reproduce the exact window with a save-changes
    // interceptor: after the revoke has read the active token(s) and staged the revoke, but the instant
    // BEFORE its SaveChanges executes, a rotation commits on a side context and bumps the parent's xmin.
    // The revoke's commit then collides and the retry-on-conflict path must self-heal. Neutering the
    // retry (letting the exception surface) makes these go red — the guard that would have caught the bug.

    private CleansiaDbContext NewContextWithRotationRace(string parentRawToken)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(new CommitRotationOnFirstSaveInterceptor(() => NewContext(), parentRawToken, StagePendingRotation))
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(UserId, $"{UserId}@cleansia.test"),
            new NullTenantProvider());
    }

    [Fact]
    public async Task RevokeAllForUser_Whose_Own_Commit_Collides_Retries_To_Success()
    {
        await ResetAsync();
        var raw = await SeedActiveTokenAsync();

        // The revoke runs on a context that races a rotation into its own read→commit window (interceptor).
        await using var revokeCtx = NewContextWithRotationRace(raw);
        await NewService(revokeCtx).RevokeAllForUserAsync(UserId, "password_reset", exceptRawToken: null, CancellationToken.None);

        // No throw, and every token for the user is dead — the account-takeover kill switch completed.
        Assert.Empty(await LiveTokensAsync());
    }

    [Fact]
    public async Task Logout_Revoke_Whose_Own_Commit_Collides_Retries_To_Success()
    {
        await ResetAsync();
        var raw = await SeedActiveTokenAsync();

        await using var revokeCtx = NewContextWithRotationRace(raw);
        // The logout revoke's own commit collides with the injected rotation and must retry to success.
        await NewService(revokeCtx).RevokeAsync(raw, "logout", CancellationToken.None);

        // The presented token is dead; the retry did not throw. (The rotation's child, a separate live
        // session, is not this single-token logout's concern.)
        var live = await LiveTokensAsync();
        var hash = NewService(revokeCtx).HashToken(raw);
        Assert.DoesNotContain(live, t => t.TokenHash == hash);
    }

    // Commits a rotation on a fresh side context exactly once, at the first SaveChanges of the intercepted
    // context — i.e. after the revoke has staged its change but before it hits the DB. This deterministically
    // opens the read→commit race window on the revoke's own commit.
    private sealed class CommitRotationOnFirstSaveInterceptor(
        Func<CleansiaDbContext> newContext,
        string parentRawToken,
        Func<CleansiaDbContext, string, RefreshToken> stageRotation) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;
                await using var rotateCtx = newContext();
                stageRotation(rotateCtx, parentRawToken);
                await rotateCtx.CommitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
