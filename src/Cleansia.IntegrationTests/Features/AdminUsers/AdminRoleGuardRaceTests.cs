using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.AdminUsers;

/// <summary>
/// ADR-0066 D4 on real Postgres. A conditional UPDATE whose WHERE asks "does another Administrator
/// remain?" is write skew against a second one on a different row: under READ COMMITTED both snapshots
/// see the other row still an Administrator, neither blocks, both land, and the company has none. The
/// two guards therefore run inside one transaction that first takes the company's advisory lock, so
/// two demotions — or two deactivations — of the last two Administrators serialise and exactly one
/// succeeds, on every run. The wedge below holds that lock from a third connection and lines both
/// calls up behind it, so the interleaving is forced rather than hoped for; the lock's own presence is
/// proven by the call that cannot finish while the wedge holds it.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AdminRoleGuardRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Company = TestTenants.Default;
    private const string AdminOne = "race-admin-1";
    private const string AdminTwo = "race-admin-2";
    private const string Actor = "race-actor";
    private const int Runs = 5;

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString() + ";Pooling=false")
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(Actor, $"{Actor}@cleansia.test"),
            new FixedTenantProvider(Company));
    }

    private async Task ResetAndSeedTwoAdministratorsAsync()
    {
        await using (var conn = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await conn.OpenAsync();
            var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToExclude = ["pg_catalog", "information_schema"],
            });
            await respawner.ResetAsync(conn);
            await SeedTenantRegistryAsync(conn);
        }

        await using var ctx = NewContext();
        ctx.Add(Language.Create("en", "English"));
        ctx.AddRange(Administrator(AdminOne), Administrator(AdminTwo));
        await ctx.CommitAsync(CancellationToken.None);
    }

    private static User Administrator(string id)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Passw0rd!", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
        user.Id = id;
        user.ConfirmEmail();
        return user;
    }

    private async Task<List<User>> AdministratorsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == AdminOne || u.Id == AdminTwo)
            .OrderBy(u => u.Id)
            .ToListAsync();
    }

    /// <summary>A third connection holding the company's advisory lock inside an open transaction.</summary>
    private async Task<(NpgsqlConnection Connection, NpgsqlTransaction Transaction)> HoldCompanyLockAsync()
    {
        var conn = new NpgsqlConnection(Fixture.GetConnectionString() + ";Pooling=false");
        await conn.OpenAsync();
        var tx = await conn.BeginTransactionAsync();
        await using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@company))", conn, tx);
        lockCommand.Parameters.AddWithValue("company", Company);
        await lockCommand.ExecuteNonQueryAsync();
        return (conn, tx);
    }

    private async Task<int> DemoteAsync(string userId)
    {
        await using var ctx = NewContext();
        return await new UserRepository(ctx).DemoteAdministratorIfAnotherRemainsAsync(Company, userId, AdminRole.Support, CancellationToken.None);
    }

    private async Task<int> DeactivateAsync(string userId)
    {
        await using var ctx = NewContext();
        return await new UserRepository(ctx).DeactivateAdministratorIfAnotherRemainsAsync(Company, userId, Actor, DateTimeOffset.UtcNow, CancellationToken.None);
    }

    [Fact]
    public async Task A_Demotion_Waits_For_The_Company_Lock()
    {
        await ResetAndSeedTwoAdministratorsAsync();
        var (conn, tx) = await HoldCompanyLockAsync();
        await using var _ = conn;

        var demotion = DemoteAsync(AdminTwo);
        var finishedWhileHeld = await Task.WhenAny(demotion, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(demotion, finishedWhileHeld);

        await tx.RollbackAsync();

        Assert.Equal(1, await demotion.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(AdminRole.Support, (await AdministratorsAsync()).Single(u => u.Id == AdminTwo).AdminRole);
    }

    [Fact]
    public async Task Two_Demotions_Of_The_Last_Two_Administrators_Leave_Exactly_One_On_Every_Run()
    {
        for (var run = 0; run < Runs; run++)
        {
            await ResetAndSeedTwoAdministratorsAsync();
            var (conn, tx) = await HoldCompanyLockAsync();
            await using var _ = conn;

            var first = DemoteAsync(AdminOne);
            var second = DemoteAsync(AdminTwo);
            await Task.Delay(200);
            await tx.RollbackAsync();
            var rows = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1, rows.Sum());
            var admins = await AdministratorsAsync();
            Assert.Single(admins, u => u.AdminRole == AdminRole.Administrator);
            Assert.Single(admins, u => u.AdminRole == AdminRole.Support);
        }
    }

    [Fact]
    public async Task Two_Deactivations_Of_The_Last_Two_Administrators_Leave_Exactly_One_On_Every_Run()
    {
        for (var run = 0; run < Runs; run++)
        {
            await ResetAndSeedTwoAdministratorsAsync();
            var (conn, tx) = await HoldCompanyLockAsync();
            await using var _ = conn;

            var first = DeactivateAsync(AdminOne);
            var second = DeactivateAsync(AdminTwo);
            await Task.Delay(200);
            await tx.RollbackAsync();
            var rows = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1, rows.Sum());
            var admins = await AdministratorsAsync();
            var survivor = Assert.Single(admins, u => u.IsActive);
            Assert.Equal(AdminRole.Administrator, survivor.AdminRole);
            var gone = Assert.Single(admins, u => !u.IsActive);
            Assert.Equal(Actor, gone.DeactivatedBy);
            Assert.NotNull(gone.DeactivatedOn);
        }
    }

    [Fact]
    public async Task A_Demotion_And_A_Deactivation_Of_The_Last_Two_Administrators_Leave_Exactly_One_Administrator()
    {
        await ResetAndSeedTwoAdministratorsAsync();
        var (conn, tx) = await HoldCompanyLockAsync();
        await using var _ = conn;

        var demotion = DemoteAsync(AdminOne);
        var deactivation = DeactivateAsync(AdminTwo);
        await Task.Delay(200);
        await tx.RollbackAsync();
        var rows = await Task.WhenAll(demotion, deactivation).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, rows.Sum());
        var admins = await AdministratorsAsync();
        Assert.Single(admins, u => u.IsActive && u.AdminRole == AdminRole.Administrator);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
