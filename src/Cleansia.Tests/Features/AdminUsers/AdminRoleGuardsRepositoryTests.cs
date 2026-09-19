using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.AdminUsers;

/// <summary>
/// The two last-Administrator guards' predicates, on a real <see cref="CleansiaDbContext"/> over
/// SQLite so the tenant filter, the correlated EXISTS and the ExecuteUpdate translate for real: a
/// demotion or deactivation lands only while ANOTHER active Administrator-role administrator of the
/// SAME company remains, a promotion needs no guard, neither guard reaches another company's row
/// however many Administrators the caller's company keeps, and the model's check constraint refuses an
/// administrator row without a role and a customer row with one. The advisory lock and the race are
/// Postgres facts and live in the integration suite.
/// </summary>
public sealed class AdminRoleGuardsRepositoryTests : IDisposable
{
    private const string Company = TestTenants.Default;
    private const string OtherCompany = TestTenants.Second;
    private const string Actor = "the-actor";

    private readonly SqliteConnection _connection;

    public AdminRoleGuardsRepositoryTests()
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
            new TestUserSessionProvider(Actor, "actor@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    private static User Admin(string id, AdminRole role, bool isActive = true)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Passw0rd!", "Ad", "Min", UserProfile.Administrator, adminRole: role);
        user.Id = id;
        user.IsActive = isActive;
        user.ConfirmEmail();
        return user;
    }

    private async Task SeedAsync(string tenantId, params User[] users)
    {
        await using var ctx = NewContext(tenantId);
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
        if (!await ctx.Set<Language>().AnyAsync(l => l.Code == "en"))
        {
            ctx.Add(Language.Create("en", "English"));
        }

        ctx.AddRange(users);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<User> LoadAsync(string id)
    {
        await using var ctx = NewContext(tenantId: null);
        return await ctx.Set<User>().IgnoreQueryFilters().AsNoTracking().FirstAsync(u => u.Id == id);
    }

    [Fact]
    public async Task Demoting_The_Only_Administrator_While_A_Support_Remains_Touches_No_Row()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator), Admin("a-support", AdminRole.Support));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "only-admin", AdminRole.Support, CancellationToken.None);

        Assert.Equal(0, rows);
        Assert.Equal(AdminRole.Administrator, (await LoadAsync("only-admin")).AdminRole);
    }

    [Fact]
    public async Task Demoting_One_Of_Two_Administrators_Lands()
    {
        await SeedAsync(Company, Admin("admin-1", AdminRole.Administrator), Admin("admin-2", AdminRole.Administrator));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "admin-2", AdminRole.Accountant, CancellationToken.None);

        Assert.Equal(1, rows);
        Assert.Equal(AdminRole.Accountant, (await LoadAsync("admin-2")).AdminRole);
        Assert.Equal(AdminRole.Administrator, (await LoadAsync("admin-1")).AdminRole);
    }

    [Fact]
    public async Task An_Inactive_Administrator_Does_Not_Count_As_Another()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator), Admin("gone-admin", AdminRole.Administrator, isActive: false));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "only-admin", AdminRole.Manager, CancellationToken.None);

        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task Another_Company_Administrator_Does_Not_Count_As_Another()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator));
        await SeedAsync(OtherCompany, Admin("their-admin", AdminRole.Administrator));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "only-admin", AdminRole.Support, CancellationToken.None);

        Assert.Equal(0, rows);
        Assert.Equal(AdminRole.Administrator, (await LoadAsync("only-admin")).AdminRole);
    }

    [Fact]
    public async Task Demoting_Another_Company_Administrator_Touches_No_Row()
    {
        await SeedAsync(Company, Admin("admin-1", AdminRole.Administrator), Admin("admin-2", AdminRole.Administrator));
        await SeedAsync(OtherCompany, Admin("their-admin", AdminRole.Administrator), Admin("their-admin-2", AdminRole.Administrator));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "their-admin", AdminRole.Support, CancellationToken.None);

        Assert.Equal(0, rows);
        Assert.Equal(AdminRole.Administrator, (await LoadAsync("their-admin")).AdminRole);
    }

    [Fact]
    public async Task Deactivating_Another_Company_Administrator_Touches_No_Row()
    {
        await SeedAsync(Company, Admin("admin-1", AdminRole.Administrator), Admin("admin-2", AdminRole.Administrator));
        await SeedAsync(OtherCompany, Admin("their-admin", AdminRole.Administrator), Admin("their-admin-2", AdminRole.Administrator));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DeactivateAdministratorIfAnotherRemainsAsync(Company, "their-admin", Actor, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(0, rows);
        var theirs = await LoadAsync("their-admin");
        Assert.True(theirs.IsActive);
        Assert.Null(theirs.DeactivatedBy);
    }

    [Fact]
    public async Task Promoting_To_Administrator_Needs_No_Other_Administrator()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator), Admin("a-support", AdminRole.Support));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "a-support", AdminRole.Administrator, CancellationToken.None);

        Assert.Equal(1, rows);
        Assert.Equal(AdminRole.Administrator, (await LoadAsync("a-support")).AdminRole);
    }

    [Fact]
    public async Task Changing_A_Support_While_An_Administrator_Remains_Lands()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator), Admin("a-support", AdminRole.Support));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, "a-support", AdminRole.Accountant, CancellationToken.None);

        Assert.Equal(1, rows);
        Assert.Equal(AdminRole.Accountant, (await LoadAsync("a-support")).AdminRole);
    }

    [Fact]
    public async Task Deactivating_The_Only_Administrator_While_A_Support_Remains_Touches_No_Row()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator), Admin("a-support", AdminRole.Support));
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DeactivateAdministratorIfAnotherRemainsAsync(Company, "only-admin", Actor, now, CancellationToken.None);

        Assert.Equal(0, rows);
        Assert.True((await LoadAsync("only-admin")).IsActive);
    }

    [Fact]
    public async Task Deactivating_A_Support_While_An_Administrator_Remains_Lands_And_Is_Stamped()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator), Admin("a-support", AdminRole.Support));
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DeactivateAdministratorIfAnotherRemainsAsync(Company, "a-support", Actor, now, CancellationToken.None);

        Assert.Equal(1, rows);
        var support = await LoadAsync("a-support");
        Assert.False(support.IsActive);
        Assert.Equal(Actor, support.DeactivatedBy);
        Assert.Equal(now, support.DeactivatedOn);
    }

    [Fact]
    public async Task Deactivating_One_Of_Two_Administrators_Lands()
    {
        await SeedAsync(Company, Admin("admin-1", AdminRole.Administrator), Admin("admin-2", AdminRole.Administrator));

        await using var ctx = NewContext(Company);
        var rows = await new UserRepository(ctx).DeactivateAdministratorIfAnotherRemainsAsync(Company, "admin-2", Actor, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(1, rows);
        Assert.False((await LoadAsync("admin-2")).IsActive);
    }

    [Fact]
    public async Task The_Model_Refuses_An_Administrator_Without_A_Role_And_A_Customer_With_One()
    {
        await SeedAsync(Company, Admin("only-admin", AdminRole.Administrator));

        await using var ctx = NewContext(Company);
        var adminWithoutRole = ctx.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Users" SET "AdminRole" = NULL WHERE "Id" = 'only-admin'
            """);
        await Assert.ThrowsAsync<SqliteException>(() => adminWithoutRole);

        var customerWithRole = ctx.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Users" SET "Profile" = 1, "AdminRole" = 3 WHERE "Id" = 'only-admin'
            """);
        await Assert.ThrowsAsync<SqliteException>(() => customerWithRole);

        Assert.Equal(AdminRole.Administrator, (await LoadAsync("only-admin")).AdminRole);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
