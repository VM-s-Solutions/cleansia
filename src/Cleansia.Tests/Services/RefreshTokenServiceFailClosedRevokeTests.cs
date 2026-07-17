using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Pins the two T-0421 revocation-race fixes at the service level, simulating xmin collisions with a
/// unit-of-work wrapper that throws <see cref="DbUpdateConcurrencyException"/> for the first N commits
/// (SQLite can't produce real xmin races; the genuine race is proven against Postgres in the
/// integration suite):
///
///  (a) Retry-budget exhaustion must NOT propagate (500 + rollback = every targeted token stays
///      alive — fail-OPEN for a kill switch). Instead the service lands a set-based bulk revoke that
///      ignores optimistic concurrency, then still commits the command's sibling staged changes
///      (modelled here by ChangePassword's staged hash). Scope narrowness is part of the contract:
///      the bulk fallback must revoke exactly the tracked path's target set — never a bystander
///      session (logout scope) and never a NULL-DeviceId row (device scope null-guard).
///
///  (b) The rotation-reuse chain revoke must RE-RUN the full chain walk on a collision, not just
///      re-revoke the conflicted entries: the racing rotation that caused the collision may have
///      committed a NEW child token the original walk never saw. Without the re-read that child
///      escapes the theft revoke alive.
/// </summary>
public sealed class RefreshTokenServiceFailClosedRevokeTests : IDisposable
{
    private const string UserId = "user-fc";
    private const string Audience = JwtAudiences.Mobile;

    private readonly SqliteConnection _connection;

    public RefreshTokenServiceFailClosedRevokeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }
        return new CleansiaDbContext(
            builder.Options,
            new TestUserSessionProvider(UserId, $"{UserId}@cleansia.test"),
            new NullTenantProvider());
    }

    private static RefreshTokenService NewService(CleansiaDbContext ctx, IUnitOfWork? unitOfWork = null)
    {
        var jwt = new Mock<IJwtSettings>();
        jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
        jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
        return new RefreshTokenService(
            new RefreshTokenRepository(ctx),
            unitOfWork ?? ctx,
            jwt.Object,
            NullLogger<RefreshTokenService>.Instance);
    }

    private async Task SeedUserAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
        ctx.Add(Language.Create("en", "English"));
        var user = User.CreateWithPassword($"{UserId}@cleansia.test", "Passw0rd!", "Fail", "Closed");
        user.Id = UserId;
        ctx.Add(user);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<string> IssueTokenAsync(string? deviceId)
    {
        await using var ctx = NewContext();
        var raw = NewService(ctx).Issue(UserId, rememberMe: true, audience: Audience, deviceId: deviceId).RawToken;
        await ctx.CommitAsync(CancellationToken.None);
        return raw;
    }

    private async Task<IReadOnlyList<RefreshToken>> GetLiveTokensAsync()
    {
        await using var ctx = NewContext();
        return await new RefreshTokenRepository(ctx).GetActiveByUserIdAsync(UserId, CancellationToken.None);
    }

    private async Task<IReadOnlyList<RefreshToken>> GetAllTokensAsync()
    {
        await using var ctx = NewContext();
        return await ctx.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == UserId)
            .ToListAsync();
    }

    // ── (a) exhaustion → set-based fail-closed revoke ──

    [Fact]
    public async Task RevokeAll_On_Retry_Exhaustion_Lands_Bulk_Revoke_And_Still_Commits_Sibling_Changes()
    {
        await SeedUserAsync();
        await IssueTokenAsync("dev-1");
        await IssueTokenAsync("dev-2");

        string? passwordBefore;
        await using (var readCtx = NewContext())
        {
            passwordBefore = (await readCtx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == UserId)).Password;
        }

        await using var ctx = NewContext();
        var uow = new CollidingUnitOfWork(ctx, failures: 5);
        var service = NewService(ctx, uow);

        // The ChangePassword shape: the new hash is staged on the SAME unit of work as the revoke-all.
        var user = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == UserId);
        user.UpdatePassword("NewPassw0rd!");

        await service.RevokeAllForUserAsync(UserId, "password_changed", exceptRawToken: null, CancellationToken.None);

        // 5 collisions consumed the retry budget; the 6th commit (after the bulk revoke) succeeded.
        Assert.Equal(6, uow.CommitCalls);
        Assert.Empty(await GetLiveTokensAsync());
        Assert.All(await GetAllTokensAsync(), t => Assert.Equal("password_changed", t.RevokedReason));

        // The sibling staged change survived the detach and landed with the final commit — the
        // fail-closed order is "revoke first, then siblings", never "give up on both". The stored
        // value is a hash (converter applies on save), so assert on change, not equality.
        await using var assertCtx = NewContext();
        var committedUser = await assertCtx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == UserId);
        Assert.NotEqual(passwordBefore, committedUser.Password);
    }

    [Fact]
    public async Task Logout_On_Retry_Exhaustion_Revokes_Only_The_Target_Token()
    {
        await SeedUserAsync();
        var target = await IssueTokenAsync("dev-1");
        await IssueTokenAsync("dev-2");

        await using var ctx = NewContext();
        var service = NewService(ctx, new CollidingUnitOfWork(ctx, failures: 5));

        await service.RevokeAsync(target, "logout", CancellationToken.None);

        // Narrow scope: the other session must survive the bulk fallback untouched.
        var live = Assert.Single(await GetLiveTokensAsync());
        Assert.Equal("dev-2", live.DeviceId);
    }

    [Fact]
    public async Task RevokeByDevice_With_A_Null_Or_Blank_DeviceId_Throws_Instead_Of_Widening()
    {
        await SeedUserAsync();
        await using var ctx = NewContext();
        var service = NewService(ctx);

        // The tracked predicate would silently match nothing, but the bulk fallback scope would
        // silently WIDEN to every session of the user (T-0421 review F2) — so the same input must
        // fail loudly on entry instead of meaning opposite things on the two paths.
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.RevokeByDeviceAsync(UserId, null!, "device_revoked", CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.RevokeByDeviceAsync(UserId, "  ", "device_revoked", CancellationToken.None));
    }

    [Fact]
    public async Task RevokeByDevice_On_Retry_Exhaustion_Spares_Null_DeviceId_Rows()
    {
        await SeedUserAsync();
        await IssueTokenAsync("dev-1");
        await IssueTokenAsync(deviceId: null);

        await using var ctx = NewContext();
        var service = NewService(ctx, new CollidingUnitOfWork(ctx, failures: 5));

        await service.RevokeByDeviceAsync(UserId, "dev-1", "device_revoked", CancellationToken.None);

        // The null-guard holds in the SQL predicate too: a token that never carried a device id is
        // not revoked by an unrelated device's kill switch.
        var live = Assert.Single(await GetLiveTokensAsync());
        Assert.Null(live.DeviceId);
    }

    [Fact]
    public async Task Bulk_Revoke_Verify_Loop_Catches_A_Token_Committed_After_The_First_Pass()
    {
        await SeedUserAsync();
        await IssueTokenAsync("dev-1");

        // The SQLite-deterministic stand-in for the Postgres statement-overlap escape (T-0421 review
        // F1): a live token committed AFTER the first bulk UPDATE completes but BEFORE the
        // verification read — exactly what a rotation flush overlapping the statement produces under
        // READ COMMITTED. The verify loop's fresh-snapshot second pass must catch it; a single-pass
        // "simplification" of BulkRevokeIgnoringConcurrencyAsync fails this test.
        var interceptor = new InjectLiveTokenOnFirstUpdateInterceptor(() => NewContext());
        await using var ctx = NewContext(interceptor);
        var repo = new RefreshTokenRepository(ctx);

        var revoked = await repo.BulkRevokeIgnoringConcurrencyAsync(
            new RefreshTokenRevocationScope { UserId = UserId }, "password_reset", CancellationToken.None);

        Assert.True(interceptor.Injected);
        Assert.Equal(2, revoked);
        Assert.Empty(await GetLiveTokensAsync());
        Assert.All(await GetAllTokensAsync(), t => Assert.Equal("password_reset", t.RevokedReason));
    }

    // ── (b) chain revoke re-read parity ──

    [Fact]
    public async Task Chain_Revoke_ReRuns_The_Walk_So_A_Race_Inserted_Child_Cannot_Escape()
    {
        await SeedUserAsync();
        var rawA = await IssueTokenAsync("dev-1");

        // Legitimate rotation: A → B. Presenting A again is now the theft signal.
        await using (var ctx = NewContext())
        {
            var service = NewService(ctx);
            await service.RotateAsync(rawA, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: "dev-1");
            await service.CommitRotationAsync(CancellationToken.None);
        }

        await using var theftCtx = NewContext();
        // On the first commit of the chain revoke, a racing rotation commits a brand-new child token
        // out-of-band (separate context = separate "transaction") and the commit collides. The old
        // code only re-revoked the conflicted entries, so this child survived the theft revoke.
        var uow = new CollidingUnitOfWork(theftCtx, failures: 1)
        {
            OnFirstFailure = () =>
            {
                using var raceCtx = NewContext();
                NewService(raceCtx).Issue(UserId, rememberMe: true, audience: Audience, deviceId: "dev-race");
                raceCtx.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
            },
        };

        await Assert.ThrowsAsync<RefreshTokenValidationException>(async () =>
            await NewService(theftCtx, uow).RotateAsync(rawA, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: "dev-1"));

        // The re-run walk caught the race-inserted child: NOTHING is left alive for this user.
        Assert.Empty(await GetLiveTokensAsync());
        var raceChild = Assert.Single(await GetAllTokensAsync(), t => t.DeviceId == "dev-race");
        Assert.Equal("security", raceChild.RevokedReason);
    }

    [Fact]
    public async Task Chain_Revoke_On_Retry_Exhaustion_Lands_Bulk_Revoke()
    {
        await SeedUserAsync();
        var rawA = await IssueTokenAsync("dev-1");

        await using (var ctx = NewContext())
        {
            var service = NewService(ctx);
            await service.RotateAsync(rawA, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: "dev-1");
            await service.CommitRotationAsync(CancellationToken.None);
        }

        await using var theftCtx = NewContext();
        var uow = new CollidingUnitOfWork(theftCtx, failures: 5);

        // The theft signal must still surface as the auth failure — never a 500 — while the kill
        // switch lands set-based despite 5 straight collisions.
        await Assert.ThrowsAsync<RefreshTokenValidationException>(async () =>
            await NewService(theftCtx, uow).RotateAsync(rawA, deviceLabel: null, ipAddress: null, CancellationToken.None, deviceId: "dev-1"));

        Assert.Equal(6, uow.CommitCalls);
        Assert.Empty(await GetLiveTokensAsync());
    }

    /// <summary>
    /// Delegates to the real context but throws a (entry-less) <see cref="DbUpdateConcurrencyException"/>
    /// for the first <c>failures</c> commits — the deterministic stand-in for an xmin collision burst.
    /// <see cref="OnFirstFailure"/> lets a test commit racing work out-of-band at the collision instant.
    /// </summary>
    private sealed class CollidingUnitOfWork(CleansiaDbContext inner, int failures) : IUnitOfWork
    {
        public int CommitCalls { get; private set; }
        public Action? OnFirstFailure { get; init; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            if (CommitCalls <= failures)
            {
                if (CommitCalls == 1)
                {
                    OnFirstFailure?.Invoke();
                }
                throw new DbUpdateConcurrencyException("simulated xmin collision");
            }
            return inner.CommitAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            inner.BeginTransactionAsync(cancellationToken);

        public void Rollback() => inner.Rollback();

        public void Dispose()
        {
            // The test owns the context's lifetime.
        }
    }

    /// <summary>
    /// After the FIRST <c>UPDATE ... RefreshTokens</c> command completes, commits a fresh live token
    /// through a nested interceptor-free context on the shared connection — landing between the bulk
    /// pass and its verification read, the deterministic reproduction of the F1 snapshot miss.
    /// </summary>
    private sealed class InjectLiveTokenOnFirstUpdateInterceptor(Func<CleansiaDbContext> newContext)
        : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Injected { get; private set; }

        public override async ValueTask<int> NonQueryExecutedAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (!Injected && command.CommandText.Contains("UPDATE") && command.CommandText.Contains("RefreshTokens"))
            {
                Injected = true;
                // Interceptor-free context: no recursion, and its own CommitAsync models the racing
                // rotation's independent transaction.
                await using var raceCtx = newContext();
                var jwt = new Mock<IJwtSettings>();
                jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
                jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
                new RefreshTokenService(
                        new RefreshTokenRepository(raceCtx), raceCtx, jwt.Object,
                        NullLogger<RefreshTokenService>.Instance)
                    .Issue(UserId, rememberMe: true, audience: Audience, deviceId: "dev-race");
                await raceCtx.CommitAsync(cancellationToken);
            }

            return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
