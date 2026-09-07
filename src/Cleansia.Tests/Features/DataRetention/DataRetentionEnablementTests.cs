using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.DataRetention;

/// <summary>
/// T-0685 — that the retention sweep RUNS on a production-shaped database, and skips only when somebody
/// switched it off on purpose.
///
/// <para><b>The failure this replaces was invisible by construction, and so was the test that guarded it.</b>
/// The sweep's master switch used to be a <c>FeatureFlags</c> row read through
/// <c>IAppConfigurationProvider.IsFeatureEnabledAsync</c>, which ends <c>globalFlag?.IsEnabled ?? false</c> —
/// an absent flag is a DISABLED flag, indistinguishable from one somebody turned off. No migration ever
/// inserted that row (there is no <c>InsertData</c>/<c>HasData</c> anywhere in the solution) and its only
/// INSERT lived in <c>sql-scripts/insert_seed_data.sql</c>, which runs solely under
/// <c>if (environment.IsDevelopment())</c> and which <c>execute-sql.yml</c> refuses against a deployed
/// database. So the sweep returned at its first line on every deployed host, logged success, and none of its
/// seven tasks had ever run — including anonymising customer PII on old orders and purging withdrawn
/// consents, which are retention obligations rather than housekeeping.</para>
///
/// <para><b>Why the old test could not catch it:</b> <c>DataRetentionFeatureFlagSeedTests</c> asserted on the
/// TEXT of the development seed file. It never opened a database, never constructed the provider and never
/// ran the sweep, so it stayed green while production did nothing — pinning the fixture rather than the
/// behaviour, which is the same mistake one layer up. This class replaces it.</para>
///
/// <para><b>Anti-vacuity conditions, so this cannot rot the same way.</b> The arrangement seeds NOTHING into
/// <c>FeatureFlags</c> — the empty table IS the production condition under test, and seeding a row here
/// would reintroduce the exact blindness. The assertions are on rows in the database, never on a log string
/// and never on a mock verification. And <see cref="Enabled_Defaults_On_So_An_Empty_Database_Cannot_Silence_The_Sweep"/>
/// is red against the pre-fix tree: the sweep would return early and the aged row would survive.</para>
/// </summary>
public sealed class DataRetentionEnablementTests : IDisposable
{
    private const string UserId = "user-retention-enablement";

    private readonly SqliteConnection _connection;
    private readonly Mock<IAppConfigurationProvider> _configProvider = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public DataRetentionEnablementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        // This mock is used by ONE test — the guard that the master switch never touches the FeatureFlags
        // table. The two behavioural tests use the real AppConfigurationProvider over this empty database,
        // because a stubbed provider is exactly what let the old tests pass while production did nothing.
        _configProvider
            .Setup(c => c.GetTenantSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// AC1 + AC3 — a production-shaped database has no <c>FeatureFlags</c> rows and no <c>DataRetention</c>
    /// configuration section. The sweep must still run. Against the pre-fix tree this fails: the absent flag
    /// resolved to false and the 100-day-old notification survived.
    /// </summary>
    [Fact]
    public async Task Enabled_Defaults_On_So_An_Empty_Database_Cannot_Silence_The_Sweep()
    {
        await EnsureSchemaAsync();
        var aged = DateTimeOffset.UtcNow.AddDays(-100);

        await using (var seed = NewContext())
        {
            seed.Add(Row(aged));
            await seed.CommitAsync(CancellationToken.None);
        }

        Assert.Empty(await ReadFlagsAsync());
        Assert.Single(await ReadNotificationsAsync());

        await using (var ctx = NewContext())
        {
            await NewSweep(ctx, EmptyConfiguration()).RunAllRetentionTasksAsync(CancellationToken.None);
        }

        Assert.Empty(await ReadNotificationsAsync());
    }

    /// <summary>
    /// AC2 — "off" is a deliberate act. With <c>DataRetention:Enabled=false</c> explicitly configured, the
    /// sweep skips and the aged row survives. The old seed-text class could not express this half at all.
    /// </summary>
    [Fact]
    public async Task Enabled_Set_False_Skips_The_Sweep()
    {
        await EnsureSchemaAsync();
        var aged = DateTimeOffset.UtcNow.AddDays(-100);

        await using (var seed = NewContext())
        {
            seed.Add(Row(aged));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var disabled = ConfigurationFrom(("DataRetention:Enabled", "false"));
            await NewSweep(ctx, disabled).RunAllRetentionTasksAsync(CancellationToken.None);
        }

        Assert.Single(await ReadNotificationsAsync());
    }

    /// <summary>
    /// The switch is bound from configuration, not read from the database. Proves the sweep no longer
    /// consults <c>IAppConfigurationProvider</c> for its master switch — the call that used to decide
    /// everything. Without this, a reintroduced flag read that happened to return true would look identical
    /// to the two tests above.
    /// </summary>
    [Fact]
    public async Task The_Master_Switch_Is_Never_Read_From_The_Feature_Flag_Table()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewSweep(ctx, EmptyConfiguration(), _configProvider.Object)
                .RunAllRetentionTasksAsync(CancellationToken.None);
        }

        _configProvider.Verify(
            c => c.IsFeatureEnabledAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Absent section binds to the shipped default, and that default is ON.</summary>
    [Fact]
    public void An_Absent_Configuration_Section_Binds_Enabled_True()
    {
        Assert.True(new DataRetentionConfig(EmptyConfiguration()).Enabled);
        Assert.False(new DataRetentionConfig(ConfigurationFrom(("DataRetention:Enabled", "false"))).Enabled);
    }

    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationFrom(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static UserNotification Row(DateTimeOffset createdOn)
    {
        var row = UserNotification.Create(UserId, NotificationEventCatalog.OrderCompleted, "{}", null);
        row.Created("seed", createdOn);
        return row;
    }

    /// <summary>
    /// The sweep over the REAL <see cref="AppConfigurationProvider"/> against this empty database — no mock
    /// stands between the test and the condition production is in. That matters: a stubbed provider is what
    /// let the old tests pass while production did nothing.
    /// </summary>
    private DataRetentionBackgroundService NewSweep(CleansiaDbContext ctx, IConfiguration configuration) =>
        NewSweep(ctx, configuration, new AppConfigurationProvider(ctx, new FixedTenantProvider(null)));

    private DataRetentionBackgroundService NewSweep(
        CleansiaDbContext ctx, IConfiguration configuration, IAppConfigurationProvider provider)
    {
        var session = new TestUserSessionProvider("system", "system@cleansia.test");
        return new DataRetentionBackgroundService(
            new UserRepository(ctx),
            new DeviceRepository(ctx, session),
            new GdprRequestRepository(ctx),
            new OrderRepository(ctx),
            new UserConsentRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new UserNotificationRepository(ctx),
            provider,
            new DataRetentionConfig(configuration),
            _blobClientFactory.Object,
            NullLogger<DataRetentionBackgroundService>.Instance);
    }

    private async Task<List<UserNotification>> ReadNotificationsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task<List<FeatureFlag>> ReadFlagsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<FeatureFlag>().IgnoreQueryFilters().ToListAsync();
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
