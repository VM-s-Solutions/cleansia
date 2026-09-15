using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.EntityConfigurations;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbAssemblyReference = Cleansia.Infra.Database.AssemblyReference;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// The seed's tenancy contract (ADR-0061 D11): applied to the MIGRATION-built schema, it leaves no
/// stamped row without an operating company, every company a row names is in the registry, and the
/// default market has an operator — otherwise every registration with no market fails
/// <c>tenant.not_found</c> in DEV, which is a seed defect and must fail here instead. Redundant with the
/// NOT NULL columns and the foreign keys into <c>Tenants</c> on purpose: this is the seed's contract,
/// and (a) proves the seed applied at all against the migration those constraints live in.
/// </summary>
[Collection("PostgresCollection")]
public sealed class SeededDatabaseHasNoOrphanTenantRowsTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private NpgsqlDataSource _dataSource = default!;

    public SeededDatabaseHasNoOrphanTenantRowsTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_fixture.GetConnectionString())
        {
            Database = "seed_tenant_closure_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await bootstrap.Database.MigrateAsync();
        }

        await using (var conn = await _dataSource.OpenConnectionAsync())
        {
            await conn.ReloadTypesAsync();
        }

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
            new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(_dataSource, x => x.MigrationsAssembly(DbAssemblyReference.Assembly))
                .Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

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

    /// <summary>(a) The seed applied — otherwise every zero below is the empty set agreeing with itself.</summary>
    [Fact]
    public async Task The_Seed_Applied_And_Registered_The_First_Operating_Company()
    {
        await using var ctx = NewContext();

        var tenant = Assert.Single(await ctx.Tenants.ToListAsync());
        Assert.Equal(TestTenants.Default, tenant.Id);
        Assert.True(await ctx.Users.IgnoreQueryFilters().AnyAsync(), "the seed did not insert the dev administrator");
        Assert.True(await ctx.EmployeePayConfigs.IgnoreQueryFilters().AnyAsync(), "the seed did not insert the pay defaults");
    }

    /// <summary>(b) and (c): no stamped row without a tenant, and every tenant named is in the registry.</summary>
    [Fact]
    public async Task No_Stamped_Table_Holds_A_Row_Outside_The_Registry()
    {
        await using var ctx = NewContext();
        var registered = (await ctx.Tenants.Select(t => t.Id).ToListAsync()).ToHashSet(StringComparer.Ordinal);

        var offenders = new List<string>();
        var scanned = 0;
        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entity.ClrType)
                || TenantAuditableEntityConfiguration<TenantAuditable, string>.TenantIdNullableTypes.Contains(entity.ClrType))
            {
                continue;
            }

            scanned++;
            var table = entity.GetTableName()!;
            var orphans = await ScalarAsync<long>($"""SELECT COUNT(*) FROM "{table}" WHERE "TenantId" IS NULL""");
            if (orphans > 0)
            {
                offenders.Add($"{table}: {orphans} row(s) with TenantId NULL");
            }

            await using var conn = await _dataSource.OpenConnectionAsync();
            await using var distinct = new NpgsqlCommand($"""SELECT DISTINCT "TenantId" FROM "{table}" WHERE "TenantId" IS NOT NULL""", conn);
            await using var reader = await distinct.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tenantId = reader.GetString(0);
                if (!registered.Contains(tenantId))
                {
                    offenders.Add($"{table}: TenantId '{tenantId}' is not in Tenants");
                }
            }
        }

        Assert.True(scanned >= 40, $"Only {scanned} stamped tables were scanned.");
        Assert.True(offenders.Count == 0, "The seed left stamped rows outside the registry:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>(d) The default market has an operator, so a registration naming no market lands somewhere.</summary>
    [Fact]
    public async Task The_Default_Market_Is_Operated_By_The_First_Company()
    {
        await using var ctx = NewContext();

        var defaultMarket = await ctx.CountryConfigurations.SingleAsync(c => c.IsDefaultMarket);
        Assert.Equal(TestTenants.Default, defaultMarket.OperatorTenantId);
    }

    /// <summary>The re-homed seed rows are the operator's, readable by a caller of that operator.</summary>
    [Fact]
    public async Task The_First_Companys_Config_Rows_Are_Its_Own()
    {
        await using var ctx = NewContext();

        Assert.NotEmpty(await ctx.CompanyInfo.ToListAsync());
        Assert.NotEmpty(await ctx.EmployeePayConfigs.ToListAsync());
        Assert.Equal(3, await ctx.PromoCodes.CountAsync());
        Assert.Equal(TestTenants.Default, (await ctx.Users.IgnoreQueryFilters().SingleAsync()).TenantId);
        Assert.Equal(4, await ctx.LoyaltyTierConfigs.CountAsync());
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
