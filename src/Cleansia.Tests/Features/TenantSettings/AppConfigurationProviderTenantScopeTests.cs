using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// The raw read behind every setting is the AMBIENT company's row: the claim on a request, the override
/// a job sets for the company it is processing. Two companies holding the same key see only their own
/// value, and a caller with neither claim nor override sees no row at all — which is what makes the
/// per-company loop in the retention job the only correct way to read a window there.
/// </summary>
public sealed class AppConfigurationProviderTenantScopeTests : IDisposable
{
    private const string Key = RetentionDefaults.CustomerAuditRetentionYearsKey;

    private readonly SqliteConnection _connection;
    private readonly FixedTenantProvider _tenantProvider = new(null);

    public AppConfigurationProviderTenantScopeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Each_Company_Reads_Its_Own_Row_Under_Its_Override_And_Nobody_Reads_Without_One()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
        await SeedAsync(ctx, TestTenants.Default, "1");
        await SeedAsync(ctx, TestTenants.Second, "7");
        var provider = new AppConfigurationProvider(ctx);

        _tenantProvider.SetTenantOverride(TestTenants.Default);
        Assert.Equal("1", await provider.GetTenantSettingAsync(Key));

        _tenantProvider.SetTenantOverride(TestTenants.Second);
        Assert.Equal("7", await provider.GetTenantSettingAsync(Key));

        _tenantProvider.ClearTenantOverride();
        Assert.Null(await provider.GetTenantSettingAsync(Key));
    }

    [Fact]
    public async Task A_Company_With_No_Row_Reads_Nothing_Even_When_Another_Company_Holds_The_Key()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
        await SeedAsync(ctx, TestTenants.Second, "7");
        var provider = new AppConfigurationProvider(ctx);

        _tenantProvider.SetTenantOverride(TestTenants.Default);

        Assert.Null(await provider.GetTenantSettingAsync(Key));
    }

    private async Task SeedAsync(CleansiaDbContext ctx, string tenantId, string value)
    {
        _tenantProvider.SetTenantOverride(tenantId);
        ctx.TenantConfigurations.Add(TenantConfiguration.Create(Key, value));
        await ctx.CommitAsync(CancellationToken.None);
        ctx.ChangeTracker.Clear();
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
