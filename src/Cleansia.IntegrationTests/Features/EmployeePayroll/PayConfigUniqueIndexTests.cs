using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The <c>EmployeePayConfigs (EmployeeId, ServiceId, PackageId)</c> unique index, against REAL
/// Postgres — the only place NULLS NOT DISTINCT exists at all.
///
/// <para><b>Why this test was a condition of the change.</b> The index used to be filtered to
/// <c>EmployeeId IS NOT NULL</c> and declared nothing about nulls, which meant it had never rejected
/// a row: every pay config carries a null by construction, since one is written per service OR per
/// package, never both. Dropping the filter and declaring NULLS NOT DISTINCT makes it enforce for the
/// first time — and the first time a constraint enforces is when you find out what was relying on it
/// not doing so.</para>
///
/// <para><b>The specific hazard.</b> <c>BulkCreateEmployeePayConfigs</c> with
/// <c>OverwriteExisting</c> issues a <c>RemoveRange</c> and an <c>AddRange</c> for the SAME key in
/// ONE commit. EF orders a delete before an insert only when it can build a uniqueness edge between
/// them, and it cannot do that for an index tuple containing a null — so the ordering falls through
/// to a comparator that happens to sort Deleted before Added. That is an undocumented implementation
/// detail, it is the load-bearing assumption behind this change, and the path has no other coverage.
/// SQLite cannot answer it: it has no NULLS NOT DISTINCT, so the whole question is invisible there.</para>
/// </summary>
[Collection("PostgresCollection")]
public class PayConfigUniqueIndexTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    // Real rows, because EmployeePayConfig has FKs to all three. The employee side is left null on
    // purpose in the platform-wide cases — that is the shape the seed writes.
    private string _currencyId = default!;
    private string _serviceId = default!;
    private string _categoryId = default!;

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

    /// <summary>Currency and service rows the pay config's foreign keys require.</summary>
    private async Task SeedCatalogueAsync()
    {
        await using var ctx = NewContext();
        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        var category = ServiceCategory.Create("payconfig-cat", "Category", "seeded");
        var service = Service.Create(category.Id, "Pay Config Service", "seeded");
        ctx.Currencies.Add(currency);
        ctx.ServiceCategories.Add(category);
        ctx.Services.Add(service);
        await ctx.CommitAsync(CancellationToken.None);
        _currencyId = currency.Id;
        _serviceId = service.Id;
        _categoryId = category.Id;
    }

    private EmployeePayConfig ForService(string? employeeId) =>
        EmployeePayConfig.CreateForService(
            _serviceId, basePay: 100m, currencyId: _currencyId, employeeId: employeeId);

    /// <summary>
    /// The whole reason for the change: two platform-wide configs for one service used to be
    /// accepted, and <c>CalculateOrderPay.SelectPreferredConfigs</c> then picks between them with
    /// <c>g.First()</c> and no ORDER BY — so a cleaner's pay would depend on Postgres row order.
    /// </summary>
    [Fact]
    public async Task Two_PlatformWide_Configs_For_One_Service_Are_Refused()
    {
        await ResetAsync();
        await SeedCatalogueAsync();

        await using (var ctx = NewContext())
        {
            ctx.Set<EmployeePayConfig>().Add(ForService(employeeId: null));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var second = NewContext();
        second.Set<EmployeePayConfig>().Add(ForService(employeeId: null));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.CommitAsync(CancellationToken.None));

        Assert.Contains("23505", ex.InnerException?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Two DIFFERENT services still get one platform-wide config each — the constraint must bind the
    /// whole tuple, not collapse every null-employee row onto one.
    /// </summary>
    [Fact]
    public async Task Different_Services_Each_Keep_Their_Own_PlatformWide_Config()
    {
        await ResetAsync();
        await SeedCatalogueAsync();

        await using var ctx = NewContext();
        var second = Service.Create(_categoryId, "Second Service", "seeded");
        ctx.Services.Add(second);
        ctx.Set<EmployeePayConfig>().Add(ForService(employeeId: null));
        ctx.Set<EmployeePayConfig>().Add(EmployeePayConfig.CreateForService(
            second.Id, basePay: 50m, currencyId: _currencyId, employeeId: null));
        await ctx.CommitAsync(CancellationToken.None);

        Assert.Equal(2, await ctx.Set<EmployeePayConfig>().CountAsync());
    }

    /// <summary>
    /// THE CONDITION. Remove-then-add of the same key inside one commit — the shape
    /// <c>BulkCreateEmployeePayConfigs</c>'s overwrite branch produces. If EF emits the INSERT before
    /// the DELETE, this is a 23505 and that branch is broken by the index change; the fix would then
    /// be to make it an UPDATE. Asserting it here rather than assuming it.
    /// </summary>
    [Fact]
    public async Task Replacing_A_Config_In_One_Commit_Orders_The_Delete_Before_The_Insert()
    {
        await ResetAsync();
        await SeedCatalogueAsync();

        await using (var seed = NewContext())
        {
            seed.Set<EmployeePayConfig>().Add(ForService(employeeId: null));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var existing = await ctx.Set<EmployeePayConfig>().SingleAsync();
        ctx.Set<EmployeePayConfig>().Remove(existing);
        ctx.Set<EmployeePayConfig>().Add(ForService(employeeId: null));

        await ctx.CommitAsync(CancellationToken.None);

        Assert.Equal(1, await ctx.Set<EmployeePayConfig>().CountAsync());
    }


    /// <summary>
    /// WHAT THE CURRENCY TERM MAKES POSSIBLE, and the reason it had to be added rather than merely
    /// wanted: without it a cleaner could hold exactly ONE rate for a service across the whole platform,
    /// so a second market could not pay anybody at all. Two platform-wide rows for one service in
    /// DIFFERENT currencies are now storable — and were not, an hour ago.
    /// </summary>
    [Fact]
    public async Task Two_PlatformWide_Configs_For_One_Service_In_Different_Currencies_Are_Allowed()
    {
        await ResetAsync();
        await SeedCatalogueAsync();

        await using var ctx = NewContext();
        var eur = Currency.Create("EUR", "E", "Euro", 1.0m);
        ctx.Currencies.Add(eur);
        ctx.Set<EmployeePayConfig>().Add(ForService(employeeId: null));
        ctx.Set<EmployeePayConfig>().Add(EmployeePayConfig.CreateForService(
            _serviceId, basePay: 12m, currencyId: eur.Id, employeeId: null));

        await ctx.CommitAsync(CancellationToken.None);

        Assert.Equal(2, await ctx.Set<EmployeePayConfig>().CountAsync());
    }

    /// <summary>
    /// And the pair is still unique WITHIN a currency — the property the old index had, kept. Widening
    /// a unique index is how one wrong answer becomes an unbounded number of them if the narrower
    /// guarantee is lost on the way.
    /// </summary>
    [Fact]
    public async Task Two_PlatformWide_Configs_For_One_Service_In_The_Same_Currency_Are_Still_Refused()
    {
        await ResetAsync();
        await SeedCatalogueAsync();

        await using (var first = NewContext())
        {
            first.Set<EmployeePayConfig>().Add(ForService(employeeId: null));
            await first.CommitAsync(CancellationToken.None);
        }

        await using var second = NewContext();
        second.Set<EmployeePayConfig>().Add(ForService(employeeId: null));

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
