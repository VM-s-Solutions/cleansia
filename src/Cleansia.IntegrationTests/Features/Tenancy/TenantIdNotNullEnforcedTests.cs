using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0061 D8, the pin: a stamped entity committed with no ambient tenant is refused by Postgres with
/// a 23502, loudly, rather than landing as a row no tenanted reader can see. This is what makes the
/// next writer that forgets its market fail on first use in DEV.
/// </summary>
[Collection("PostgresCollection")]
public sealed class TenantIdNotNullEnforcedTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private readonly PostgresContainerFixture _fixture = fixture;

    private CleansiaDbContext NewContext(string? tenantId) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_fixture.GetConnectionString()).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"),
        new FixedTenantProvider(tenantId));

    [Fact]
    public async Task A_Stamped_Row_Committed_Under_No_Tenant_Raises_23502()
    {
        await using (var reset = new NpgsqlConnection(_fixture.GetConnectionString()))
        {
            await reset.OpenAsync();
            await using var truncate = new NpgsqlCommand("""TRUNCATE "Users", "Languages" CASCADE""", reset);
            await truncate.ExecuteNonQueryAsync();
            await SeedTenantRegistryAsync(reset);
        }

        await using (var seed = NewContext(TestTenants.Default))
        {
            seed.Languages.Add(Language.Create("en", "English"));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext(tenantId: null);
        ctx.Users.Add(User.CreateWithPassword("no-tenant@cleansia.test", "Passw0rd!", "No", "Tenant"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.NotNullViolation, postgres.SqlState);
        Assert.Equal("TenantId", postgres.ColumnName);
    }

    [Fact]
    public async Task The_Same_Row_Committed_Under_A_Tenant_Lands_Stamped()
    {
        await using (var registry = new NpgsqlConnection(_fixture.GetConnectionString()))
        {
            await registry.OpenAsync();
            await SeedTenantRegistryAsync(registry);
        }

        await using (var seed = NewContext(TestTenants.Default))
        {
            if (!await seed.Languages.AnyAsync(l => l.Code == "en"))
            {
                seed.Languages.Add(Language.Create("en", "English"));
                await seed.CommitAsync(CancellationToken.None);
            }
        }

        await using var ctx = NewContext(TestTenants.Second);
        var user = User.CreateWithPassword("stamped@cleansia.test", "Passw0rd!", "Stam", "Ped");
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);

        await using var verify = NewContext(tenantId: null);
        Assert.Equal(TestTenants.Second, (await verify.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id)).TenantId);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
