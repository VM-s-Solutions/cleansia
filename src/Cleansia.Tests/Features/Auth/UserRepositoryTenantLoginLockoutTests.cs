using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Proves the anonymous login/lockout/reset path reaches tenant-stamped accounts. Login, lockout
/// counting and password-reset all run on ANONYMOUS requests (no tenant claim), so the ambient tenant
/// is null and the global tenant filter narrows every read to <c>TenantId == null</c> — a user carrying
/// a real TenantId could never be found by the login lookups, never accrue failed-login counts (the
/// lockout ExecuteUpdate matched zero rows), and never charge the reset-code budget. Mirrors the
/// RefreshTokenServiceTenantRevokeTests arrangement: a REAL <see cref="CleansiaDbContext"/> over SQLite
/// in-memory so the global tenant query filter actually runs.
/// </summary>
public sealed class UserRepositoryTenantLoginLockoutTests : IDisposable
{
    private const string Tenant = "tenant-1";
    private const string TenantUserId = "user-tenant";
    private const string TenantEmail = "tenant-user@cleansia.test";
    private const string NullTenantUserId = "user-null";
    private const string NullTenantEmail = "null-user@cleansia.test";

    private readonly SqliteConnection _connection;

    public UserRepositoryTenantLoginLockoutTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    /// <summary>
    /// Seeds one null-tenant user (single-tenant baseline) and one tenant-stamped user. The tenant
    /// user is committed under a tenant-carrying context so <c>CommitAsync</c> stamps its TenantId —
    /// exactly what a migrated multi-tenant account looks like.
    /// </summary>
    private async Task SeedAsync()
    {
        await using (var ctx = NewContext(tenantId: null))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Add(Language.Create("en", "English"));

            var nullTenantUser = User.CreateWithPassword(NullTenantEmail, "Passw0rd!", "Null", "Tenant");
            nullTenantUser.Id = NullTenantUserId;
            ctx.Add(nullTenantUser);

            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext(Tenant))
        {
            var tenantUser = User.CreateWithPassword(TenantEmail, "Passw0rd!", "Tenant", "Stamped");
            tenantUser.Id = TenantUserId;
            ctx.Add(tenantUser);

            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext(tenantId: null);
        var stamped = await verify.Set<User>().IgnoreQueryFilters().FirstAsync(u => u.Id == TenantUserId);
        Assert.Equal(Tenant, stamped.TenantId);
    }

    private async Task<User> LoadIgnoringFiltersAsync(string userId)
    {
        await using var ctx = NewContext(tenantId: null);
        return await ctx.Set<User>().IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
    }

    [Fact]
    public async Task GetByEmailIgnoringTenantAsync_AnonymousRequest_FindsTheTenantStampedUser()
    {
        await SeedAsync();

        await using var ctx = NewContext(tenantId: null);
        var user = await new UserRepository(ctx).GetByEmailIgnoringTenantAsync(TenantEmail, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(TenantUserId, user.Id);
    }

    [Fact]
    public async Task GetByEmailIgnoringTenantAsync_AnonymousRequest_StillFindsTheNullTenantUser()
    {
        await SeedAsync();

        await using var ctx = NewContext(tenantId: null);
        var user = await new UserRepository(ctx).GetByEmailIgnoringTenantAsync(NullTenantEmail, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(NullTenantUserId, user.Id);
    }

    [Fact]
    public async Task ExistsWithEmailIgnoringTenantAsync_AnonymousRequest_SeesBothUsers()
    {
        await SeedAsync();

        await using var ctx = NewContext(tenantId: null);
        var repository = new UserRepository(ctx);

        Assert.True(await repository.ExistsWithEmailIgnoringTenantAsync(TenantEmail, CancellationToken.None));
        Assert.True(await repository.ExistsWithEmailIgnoringTenantAsync(NullTenantEmail, CancellationToken.None));
        Assert.False(await repository.ExistsWithEmailIgnoringTenantAsync("nobody@cleansia.test", CancellationToken.None));
    }

    // Pins WHY the login path needs the bypass: the tenant-filtered lookup cannot see the
    // tenant-stamped account on an anonymous request. If this ever starts finding the user, the
    // ambient-tenant resolution changed and the bypass methods should be revisited.
    [Fact]
    public async Task GetByEmailAsync_AnonymousRequest_CannotSeeTheTenantStampedUser()
    {
        await SeedAsync();

        await using var ctx = NewContext(tenantId: null);
        var user = await new UserRepository(ctx).GetByEmailAsync(TenantEmail, CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task RecordFailedLoginAsync_AnonymousRequest_IncrementsTheTenantStampedUsersCounter()
    {
        await SeedAsync();

        await using (var ctx = NewContext(tenantId: null))
        {
            await new UserRepository(ctx).RecordFailedLoginAsync(TenantEmail, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var user = await LoadIgnoringFiltersAsync(TenantUserId);
        Assert.Equal(1, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task RecordFailedLoginAsync_AnonymousRequest_StillIncrementsTheNullTenantUser()
    {
        await SeedAsync();

        await using (var ctx = NewContext(tenantId: null))
        {
            await new UserRepository(ctx).RecordFailedLoginAsync(NullTenantEmail, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var user = await LoadIgnoringFiltersAsync(NullTenantUserId);
        Assert.Equal(1, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task TryChargeResetPasswordCodeAttemptAsync_AnonymousRequest_ChargesTheTenantStampedUsersBudget()
    {
        await SeedAsync();

        bool charged;
        await using (var ctx = NewContext(tenantId: null))
        {
            charged = await new UserRepository(ctx).TryChargeResetPasswordCodeAttemptAsync(TenantUserId, CancellationToken.None);
        }

        Assert.True(charged);
        var user = await LoadIgnoringFiltersAsync(TenantUserId);
        Assert.Equal(1, user.ResetPasswordCodeAttempts);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
