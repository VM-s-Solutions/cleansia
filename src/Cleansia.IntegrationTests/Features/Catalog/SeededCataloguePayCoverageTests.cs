using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Catalog;

/// <summary>
/// The seed is the first thing the pay gate judges. A fresh Development boot executes
/// <c>insert_seed_data.sql</c> and DEV is dropped and re-seeded rather than migrated, so if the seeded
/// catalogue has entries with no platform-wide <c>EmployeePayConfig</c> then every cleaner on a fresh
/// database opens the app to a board of blank pay and no admin can be approved at all — the gate would
/// reject the platform's own starting state.
///
/// <para>Executed against real PostgreSQL because that is the only thing that runs the script: a text
/// assertion over the SQL proves the INSERT was typed, not that it covers every row the same file
/// seeds two hundred lines earlier.</para>
/// </summary>
[Collection("PostgresCollection")]
public class SeededCataloguePayCoverageTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private NpgsqlDataSource _dataSource = default!;

    public SeededCataloguePayCoverageTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_fixture.GetConnectionString())
        {
            Database = "seed_pay_coverage_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using (var conn = await _dataSource.OpenConnectionAsync())
        {
            await conn.ReloadTypesAsync();
        }

        // Executed on the raw connection, exactly as CleansiaStartupBase.SeedDevelopmentData does:
        // the script's JSON translation columns carry braces, which EF's raw-SQL builder parses as
        // format placeholders.
        await using var seedConnection = await _dataSource.OpenConnectionAsync();
        await using var seedCommand = seedConnection.CreateCommand();
        seedCommand.CommandText = ReadCanonicalSeedScript();
        seedCommand.CommandTimeout = 120;
        await seedCommand.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }
        await _dataSource.DisposeAsync();
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private static string ReadCanonicalSeedScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the solution directory from the test base directory.");
        return File.ReadAllText(
            Path.GetFullPath(Path.Combine(dir!.FullName, "..", "sql-scripts", "insert_seed_data.sql")));
    }

    private Task<IReadOnlyList<PayCoverageTarget>> PlatformWideGapsAsync(CleansiaDbContext ctx) =>
        PayCoverageLookup.FindActiveCatalogueGapsAsync(
            new ServiceRepository(ctx),
            new PackageRepository(ctx),
            new EmployeePayConfigRepository(ctx),
            employeeId: null,
            CancellationToken.None);

    /// <summary>
    /// Anti-vacuity first: the script really did seed a catalogue, so "no gaps" below is not the empty
    /// set agreeing with itself.
    /// </summary>
    [Fact]
    public async Task The_Script_Really_Seeds_A_Catalogue()
    {
        await using var ctx = NewContext();

        Assert.True(await ctx.Services.CountAsync(s => s.IsActive) >= 10);
        Assert.True(await ctx.Packages.CountAsync(p => p.IsActive) >= 8);
    }

    [Fact]
    public async Task Every_Seeded_Catalogue_Entry_Has_A_Platform_Wide_Pay_Config()
    {
        await using var ctx = NewContext();

        var gaps = await PlatformWideGapsAsync(ctx);

        Assert.Equal([], gaps.Select(gap => $"{gap.Kind}:{gap.Name}").Order().ToList());
    }

    /// <summary>
    /// Every seeded config is the platform-wide row, not a per-employee override — an override would
    /// leave the entry unquotable for everybody else and the gate would (correctly) still refuse.
    /// </summary>
    [Fact]
    public async Task Every_Seeded_Pay_Config_Is_Platform_Wide()
    {
        await using var ctx = NewContext();

        Assert.False(await ctx.EmployeePayConfigs.AnyAsync(c => c.EmployeeId != null));
        Assert.True(await ctx.EmployeePayConfigs.AnyAsync());
    }

    /// <summary>
    /// The reachable sequence, end to end against the real seeded database: publish a service the way
    /// <c>CreateService</c> does — <c>IsActive</c> true, no pay config — and it is neither offered by the
    /// booking wizard nor coverable, so no order can carry it.
    /// </summary>
    [Fact]
    public async Task A_Freshly_Published_Service_With_No_Pay_Config_Is_A_Gap_And_Is_Not_Offered()
    {
        string publishedId;
        await using (var publishContext = NewContext())
        {
            var categoryId = await publishContext.ServiceCategories.Select(c => c.Id).FirstAsync();
            var published = Service.Create(categoryId, "Brand New Service", "just published", 900m, 100m, 60);
            publishContext.Services.Add(published);
            await publishContext.CommitAsync(CancellationToken.None);
            publishedId = published.Id;
        }

        await using var ctx = NewContext();

        var gap = Assert.Single(await PlatformWideGapsAsync(ctx));
        Assert.Equal(publishedId, gap.Id);
        Assert.Equal("Brand New Service", gap.Name);

        var offered = await new GetServiceOverview.Handler(
                new ServiceRepository(ctx), new EmployeePayConfigRepository(ctx))
            .Handle(new GetServiceOverview.Request(), CancellationToken.None);

        Assert.DoesNotContain(offered, item => item.Id == publishedId);
        Assert.NotEmpty(offered);
    }

    /// <summary>
    /// The other direction, on the seeded data: a cleaner with no personal configs at all is fully
    /// covered by the seeded platform-wide set, which is what makes approval possible on a fresh
    /// database without configuring anybody individually first.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_With_No_Personal_Configs_Has_No_Gaps_Against_The_Seeded_Catalogue()
    {
        await using var ctx = NewContext();

        var gaps = await PayCoverageLookup.FindActiveCatalogueGapsAsync(
            new ServiceRepository(ctx),
            new PackageRepository(ctx),
            new EmployeePayConfigRepository(ctx),
            employeeId: "a-cleaner-with-nothing-of-their-own",
            CancellationToken.None);

        Assert.Empty(gaps);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
