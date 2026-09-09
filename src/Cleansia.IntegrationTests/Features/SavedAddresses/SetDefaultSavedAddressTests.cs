using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.SavedAddresses;

/// <summary>
/// Changing which saved address is the default. Against REAL Postgres, because the thing that decides
/// the outcome is a partial unique index that only Postgres enforces.
///
/// <para><b>This handler could not succeed, and had no test of any kind — in any project.</b>
/// <c>IX_SavedAddresses_UserId_Default_Unique</c> is a partial unique index, so Postgres cannot defer it
/// and checks it at the end of every statement: the instant two of one user's addresses are default is a
/// violation, not an intermediate state. EF emits the two UPDATEs in the order the entities entered the
/// CHANGE TRACKER rather than the order they were mutated, and the handler loaded the promote target
/// first — so the promote was always emitted first and every attempt raised an unhandled 23505.</para>
///
/// <para>That is a 500 on <c>SetDefault</c> for a customer fixing their own saved addresses, on both the
/// web and the mobile customer hosts. It was found by the adversarial review of
/// <c>SetDefaultCurrency</c>, which has the identical shape — not by the suite, which never touched this
/// handler.</para>
///
/// <para><b>These tests discriminate, and the commit is why.</b> Each one runs the handler and then
/// performs the pipeline's own <c>CommitAsync</c>, because that is where the violation lands: the old
/// handler only tracked its two mutations, so a test that called the handler and asserted without
/// committing would pass against broken code — it would be asserting that nothing happened. Run against
/// the pre-fix handler these three fail with <c>23505: duplicate key value violates unique constraint
/// "IX_SavedAddresses_UserId"</c>, which is exactly the customer-facing 500.</para>
/// </summary>
[Collection("PostgresCollection")]
public class SetDefaultSavedAddressTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string UserId = "user-saved-default";

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(UserId, "saved-default@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
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

    /// <summary>Two saved addresses on one user; the FIRST is the default. Returns (first, second) ids.</summary>
    private async Task<(string First, string Second)> SeedAsync()
    {
        await using var ctx = NewContext();

        ctx.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        ctx.Countries.Add(country);

        var user = User.CreateWithPassword(
            "saved-default@cleansia.test", "Test-password-1!", "Saved", "Default", UserProfile.Customer);
        user.Id = UserId;
        ctx.Add(user);

        var addressOne = Address.Create("Dlouha 1", "Praha", "11000", country.Id);
        var addressTwo = Address.Create("Dlouha 2", "Praha", "11000", country.Id);
        ctx.AddRange(addressOne, addressTwo);

        var first = SavedAddress.Create(UserId, addressOne.Id, "Home", isDefault: true);
        var second = SavedAddress.Create(UserId, addressTwo.Id, "Work", isDefault: false);
        ctx.AddRange(first, second);

        await ctx.CommitAsync(CancellationToken.None);
        return (first.Id, second.Id);
    }

    /// <summary>The real handler, same rows, same direction — succeeds, and leaves exactly one default.</summary>
    [Fact]
    public async Task The_Handler_Moves_The_Default()
    {
        await ResetAsync();
        var (first, second) = await SeedAsync();

        await using (var ctx = NewContext())
        {
            var result = await new SetDefaultSavedAddress.Handler(new SavedAddressRepository(ctx, new TestUserSessionProvider(UserId, "saved-default@cleansia.test")))
                .Handle(new SetDefaultSavedAddress.Command(second), CancellationToken.None);

            Assert.True(result.IsSuccess, $"SetDefaultSavedAddress failed with: {result.Error?.Message}");

            // What UnitOfWorkPipelineBehavior does after a successful command. Without it the handler's
            // tracked mutations never reach the database, and a test that omits it proves only that the
            // handler committed something itself.
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext();
        var defaults = await verify.Set<SavedAddress>().Where(s => s.UserId == UserId && s.IsDefault).ToListAsync();
        Assert.Equal(second, Assert.Single(defaults).Id);
        Assert.False((await verify.Set<SavedAddress>().SingleAsync(s => s.Id == first)).IsDefault);
    }

    /// <summary>
    /// And back again. A customer toggling between two addresses is the sequence that made the old shape
    /// look intermittent to anyone who happened to test only one direction.
    /// </summary>
    [Fact]
    public async Task The_Handler_Moves_The_Default_Back()
    {
        await ResetAsync();
        var (first, second) = await SeedAsync();

        foreach (var target in new[] { second, first })
        {
            await using var ctx = NewContext();
            var result = await new SetDefaultSavedAddress.Handler(new SavedAddressRepository(ctx, new TestUserSessionProvider(UserId, "saved-default@cleansia.test")))
                .Handle(new SetDefaultSavedAddress.Command(target), CancellationToken.None);

            Assert.True(result.IsSuccess, $"promoting {target} failed with: {result.Error?.Message}");
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext();
        var defaults = await verify.Set<SavedAddress>().Where(s => s.UserId == UserId && s.IsDefault).ToListAsync();
        Assert.Equal(first, Assert.Single(defaults).Id);
    }

    /// <summary>
    /// Promoting the address that is already default is a no-op rather than a self-collision — the clear
    /// takes it to false and the promote puts it back, and no statement leaves two rows true.
    /// </summary>
    [Fact]
    public async Task Promoting_The_Current_Default_Leaves_It_Default()
    {
        await ResetAsync();
        var (first, _) = await SeedAsync();

        await using (var ctx = NewContext())
        {
            var result = await new SetDefaultSavedAddress.Handler(new SavedAddressRepository(ctx, new TestUserSessionProvider(UserId, "saved-default@cleansia.test")))
                .Handle(new SetDefaultSavedAddress.Command(first), CancellationToken.None);

            Assert.True(result.IsSuccess, $"SetDefaultSavedAddress failed with: {result.Error?.Message}");

            // What UnitOfWorkPipelineBehavior does after a successful command. Without it the handler's
            // tracked mutations never reach the database, and a test that omits it proves only that the
            // handler committed something itself.
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext();
        var defaults = await verify.Set<SavedAddress>().Where(s => s.UserId == UserId && s.IsDefault).ToListAsync();
        Assert.Equal(first, Assert.Single(defaults).Id);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
