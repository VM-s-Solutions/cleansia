using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Company;

/// <summary>
/// One legal entity may hold a company row PER COUNTRY, against real Postgres.
///
/// <para>The unique index was on <c>RegistrationNumber</c> alone, so one entity could not have a second
/// country's row — while the read path has always been per-country (<c>OrderFactory</c>,
/// <c>ReceiptService</c> and <c>RegenerateInvoicePdf</c> all resolve by country and fall back to any
/// active row when none matches). The composite accommodates both futures the owner could not choose
/// between — one entity across several markets, or one entity per market — and the global unique
/// accommodated neither.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CompanyInfoCountryScopeTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string RegistrationNumber = "12345678";

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
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

    private async Task<(string Cz, string Sk)> SeedCountriesAsync()
    {
        await using var ctx = NewContext();
        var cz = Country.Create("Czechia", "CZ", isServiced: true);
        var sk = Country.Create("Slovakia", "SK", isServiced: false);
        ctx.Countries.AddRange(cz, sk);
        await ctx.CommitAsync(CancellationToken.None);
        return (cz.Id, sk.Id);
    }

    private static CompanyInfo Company(string countryId) =>
        CompanyInfo.Create(
            "Cleansia s.r.o.", "Cleansia", RegistrationNumber,
            "Dlouha 1", "Praha", "11000", countryId);

    /// <summary>
    /// THE STATE THAT WAS UNREPRESENTABLE. One registration number, two countries — refused by the old
    /// index, which is what made "one legal entity, several markets" impossible to store.
    /// </summary>
    [Fact]
    public async Task One_Entity_Can_Hold_A_Row_Per_Country()
    {
        await ResetAsync();
        var (cz, sk) = await SeedCountriesAsync();

        await using var ctx = NewContext();
        ctx.Add(Company(cz));
        ctx.Add(Company(sk));
        await ctx.CommitAsync(CancellationToken.None);

        Assert.Equal(2, await ctx.Set<CompanyInfo>().CountAsync(c => c.RegistrationNumber == RegistrationNumber));
    }

    /// <summary>
    /// And the pair is still unique. Widening a unique index loses its narrower guarantee unless the new
    /// one is checked in both directions.
    /// </summary>
    [Fact]
    public async Task The_Same_Entity_Cannot_Hold_Two_Rows_For_One_Country()
    {
        await ResetAsync();
        var (cz, _) = await SeedCountriesAsync();

        await using (var first = NewContext())
        {
            first.Add(Company(cz));
            await first.CommitAsync(CancellationToken.None);
        }

        await using var second = NewContext();
        second.Add(Company(cz));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
