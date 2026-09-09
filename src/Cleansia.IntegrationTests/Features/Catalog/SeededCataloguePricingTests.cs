using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Catalog;

/// <summary>
/// Every active seeded catalogue entry has a price row in the default currency, and nothing has one in
/// any other currency.
///
/// <para>This is the guard the seed's own comment names, and it exists because the property it protects
/// stopped being structural. Prices used to be a column beside the entry, so a catalogue row without a
/// price was unrepresentable; under the owner's Option B ruling they are AUTHORED per currency, and the
/// price block joins the catalogue by name. A rename, a typo or a new entry added above but not below
/// now seeds nothing at all for that row — no error, no null, just an entry the booking wizard quietly
/// withholds, because "no row" means "not offerable in this currency" everywhere in the system.</para>
///
/// <para>The second half is the scope ruling made provable. Owner ruling 2026-09-08: build the
/// multicurrency machinery now, operate only CZK. EUR is seeded inactive with no prices, so an entry
/// carrying a EUR price would mean somebody had started a market that has not been decided — and it
/// would be visible nowhere else, since nothing reads EUR yet.</para>
///
/// <para>Real PostgreSQL because the seed script is the subject: a text assertion over the SQL proves
/// the INSERT was typed, not that the name in it matches a row two hundred lines above.</para>
/// </summary>
[Collection("PostgresCollection")]
public class SeededCataloguePricingTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private NpgsqlDataSource _dataSource = default!;

    public SeededCataloguePricingTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_fixture.GetConnectionString())
        {
            Database = "seed_pricing_test"
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

        // Raw connection, exactly as CleansiaStartupBase.SeedDevelopmentData does: the script's JSON
        // translation columns carry braces, which EF's raw-SQL builder parses as format placeholders.
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

    private static Task<string> DefaultCurrencyIdAsync(CleansiaDbContext ctx) =>
        ctx.Currencies.Where(c => c.IsDefault).Select(c => c.Id).SingleAsync();

    /// <summary>
    /// Anti-vacuity first: the script really did seed a catalogue AND prices for it, so the "nothing is
    /// missing" assertions below are not two empty sets agreeing with each other.
    /// </summary>
    [Fact]
    public async Task The_Seed_Produces_A_Priced_Catalogue()
    {
        await using var ctx = NewContext();

        Assert.Equal(10, await ctx.Services.CountAsync());
        Assert.Equal(8, await ctx.Packages.CountAsync());
        Assert.Equal(5, await ctx.Extras.CountAsync());

        Assert.Equal(10, await ctx.ServicePrices.CountAsync());
        Assert.Equal(8, await ctx.PackagePrices.CountAsync());
        Assert.Equal(5, await ctx.ExtraPrices.CountAsync());
    }

    [Fact]
    public async Task Every_Active_Service_Has_A_Price_In_The_Default_Currency()
    {
        await using var ctx = NewContext();
        var currencyId = await DefaultCurrencyIdAsync(ctx);

        var unpriced = await ctx.Services
            .Where(s => s.IsActive)
            .Where(s => !ctx.ServicePrices.Any(p => p.ServiceId == s.Id && p.CurrencyId == currencyId))
            .Select(s => s.Name)
            .ToListAsync();

        Assert.Equal([], unpriced);
    }

    [Fact]
    public async Task Every_Active_Package_Has_A_Price_In_The_Default_Currency()
    {
        await using var ctx = NewContext();
        var currencyId = await DefaultCurrencyIdAsync(ctx);

        var unpriced = await ctx.Packages
            .Where(p => p.IsActive)
            .Where(p => !ctx.PackagePrices.Any(pp => pp.PackageId == p.Id && pp.CurrencyId == currencyId))
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Equal([], unpriced);
    }

    [Fact]
    public async Task Every_Active_Extra_Has_A_Price_In_The_Default_Currency()
    {
        await using var ctx = NewContext();
        var currencyId = await DefaultCurrencyIdAsync(ctx);

        var unpriced = await ctx.Extras
            .Where(e => e.IsActive)
            .Where(e => !ctx.ExtraPrices.Any(ep => ep.ExtraId == e.Id && ep.CurrencyId == currencyId))
            .Select(e => e.Slug)
            .ToListAsync();

        Assert.Equal([], unpriced);
    }

    /// <summary>
    /// Zero prices outside the default currency. EUR exists so the schema and the admin surfaces have a
    /// second currency to be exercised against, and it is seeded INACTIVE and unpriced precisely so the
    /// claim "only CZK is operated" has something enforcing it.
    /// </summary>
    [Fact]
    public async Task No_Catalogue_Entry_Is_Priced_Outside_The_Default_Currency()
    {
        await using var ctx = NewContext();
        var currencyId = await DefaultCurrencyIdAsync(ctx);

        Assert.False(await ctx.ServicePrices.AnyAsync(p => p.CurrencyId != currencyId));
        Assert.False(await ctx.PackagePrices.AnyAsync(p => p.CurrencyId != currencyId));
        Assert.False(await ctx.ExtraPrices.AnyAsync(p => p.CurrencyId != currencyId));
    }

    /// <summary>
    /// The seeded pay is HALF the seeded price, in the same currency, because the seed derives one from
    /// the other. Checked on the amounts rather than on the SQL text: a join that reads the right table
    /// and multiplies by the wrong number, or stamps a currency from somewhere else, passes every
    /// text assertion in <c>PayConfigSeedTests</c> and fails here.
    /// </summary>
    [Fact]
    public async Task Seeded_Service_Pay_Is_Half_The_Seeded_Price_In_The_Same_Currency()
    {
        await using var ctx = NewContext();

        var mismatched = await (
            from config in ctx.EmployeePayConfigs
            where config.ServiceId != null && config.EmployeeId == null
            join price in ctx.ServicePrices
                on new { Id = config.ServiceId!, config.CurrencyId }
                equals new { Id = price.ServiceId, price.CurrencyId }
            where config.BasePay != Math.Round(price.BasePrice * 0.5m, 2)
                  || config.ExtraPerRoom != Math.Round(price.PerRoomPrice * 0.5m, 2)
            select config.Id).ToListAsync();

        Assert.Equal([], mismatched);

        // ...and every config found a price row to be judged against, rather than the join being empty.
        Assert.Equal(
            await ctx.EmployeePayConfigs.CountAsync(c => c.ServiceId != null && c.EmployeeId == null),
            await ctx.ServicePrices.CountAsync());
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
