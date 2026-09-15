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
    private const string Tenant = TestTenants.Second;
    private const string TenantUserId = "user-tenant";
    private const string TenantEmail = "tenant-user@cleansia.test";
    private const string DefaultTenantUserId = "user-default";
    private const string DefaultTenantEmail = "default-user@cleansia.test";

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
    /// Seeds one user of the default operating company and one of another tenant. Each is committed
    /// under its tenant's context so <c>CommitAsync</c> stamps its TenantId — exactly what two
    /// companies' accounts look like (ADR-0061 D8: every row is stamped).
    /// </summary>
    private async Task SeedAsync()
    {
        await using (var ctx = NewContext(TestTenants.Default))
        {
            await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
            ctx.Add(Language.Create("en", "English"));

            var nullTenantUser = User.CreateWithPassword(DefaultTenantEmail, "Passw0rd!", "Null", "Tenant");
            nullTenantUser.Id = DefaultTenantUserId;
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
    public async Task GetByEmailIgnoringTenantAsync_AnonymousRequest_StillFindsTheDefaultTenantUser()
    {
        await SeedAsync();

        await using var ctx = NewContext(tenantId: null);
        var user = await new UserRepository(ctx).GetByEmailIgnoringTenantAsync(DefaultTenantEmail, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(DefaultTenantUserId, user.Id);
    }

    [Fact]
    public async Task ExistsWithEmailIgnoringTenantAsync_AnonymousRequest_SeesBothUsers()
    {
        await SeedAsync();

        await using var ctx = NewContext(tenantId: null);
        var repository = new UserRepository(ctx);

        Assert.True(await repository.ExistsWithEmailIgnoringTenantAsync(TenantEmail, CancellationToken.None));
        Assert.True(await repository.ExistsWithEmailIgnoringTenantAsync(DefaultTenantEmail, CancellationToken.None));
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
    public async Task RecordFailedLoginAsync_AnonymousRequest_StillIncrementsTheDefaultTenantUser()
    {
        await SeedAsync();

        await using (var ctx = NewContext(tenantId: null))
        {
            await new UserRepository(ctx).RecordFailedLoginAsync(DefaultTenantEmail, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var user = await LoadIgnoringFiltersAsync(DefaultTenantUserId);
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
