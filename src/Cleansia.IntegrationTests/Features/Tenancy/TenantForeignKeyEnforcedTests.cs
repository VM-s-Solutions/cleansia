using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// The registry is a real constraint, the pin: a stamped row committed under a tenant id that is not an
/// operating company is refused by Postgres with a 23503 naming the table's FK into <c>Tenants</c>,
/// loudly, rather than landing as a row no company's reader can see.
/// </summary>
[Collection("PostgresCollection")]
public sealed class TenantForeignKeyEnforcedTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private readonly PostgresContainerFixture _fixture = fixture;

    private CleansiaDbContext NewContext(string? tenantId) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_fixture.GetConnectionString()).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"),
        new FixedTenantProvider(tenantId));

    [Fact]
    public async Task A_Stamped_Row_Committed_Under_An_Unregistered_Tenant_Raises_23503()
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

        await using var ctx = NewContext(tenantId: "not-a-company");
        ctx.Users.Add(User.CreateWithPassword("stranger@cleansia.test", "Passw0rd!", "No", "Company"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal("FK_Users_Tenants_TenantId", postgres.ConstraintName);

        await using var verify = NewContext(tenantId: null);
        Assert.False(await verify.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == "stranger@cleansia.test"));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
