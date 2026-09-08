using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
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
/// T-0685 — that the retention sweep RUNS when nobody has configured anything, and skips only when
/// somebody switched it off on purpose.
///
/// <para><b>The failure this replaces was invisible by construction, and so was the test that guarded it.</b>
/// The sweep's master switch used to be a row in a <c>FeatureFlags</c> database table, read through a
/// provider that resolved a missing row to <c>false</c> — so an absent flag was a DISABLED flag,
/// indistinguishable from one somebody turned off. No migration ever inserted that row and its only INSERT
/// lived in the development-only seed, which CI refuses to run against production. The sweep therefore
/// returned at its first line on every deployed host, logged success, and none of its seven tasks had ever
/// run — including anonymising customer PII on old orders and purging withdrawn consents, which are
/// retention obligations rather than housekeeping.</para>
///
/// <para><b>Why the old test could not catch it:</b> <c>DataRetentionFeatureFlagSeedTests</c> asserted on the
/// TEXT of the development seed file. It never opened a database and never ran the sweep, so it stayed green
/// while production did nothing — pinning the fixture rather than the behaviour. This class replaced it.</para>
///
/// <para><b>The flag table itself is gone (T-0689)</b>, along with the whole feature-flag mechanism, which
/// gated nothing once this switch left it. Two assertions that lived here — that the sweep never consulted
/// the flag table, and that the table was empty — went with it: they can no longer be expressed, and no
/// longer need to be, because the table a reader might worry about does not exist. What remains is the
/// behaviour that actually matters, and it is unchanged.</para>
///
/// <para><b>Anti-vacuity conditions, so this cannot rot the way its predecessor did.</b> The assertions are
/// on rows in the database, never on a log string and never on a mock verification, and
/// <see cref="Enabled_Defaults_On_When_Nothing_Is_Configured"/> was verified RED against the pre-fix tree:
/// the sweep returned early and the aged row survived.</para>
/// </summary>
public sealed class DataRetentionEnablementTests : IDisposable
{
    private const string UserId = "user-retention-enablement";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public DataRetentionEnablementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// AC1 — a deployed database has no <c>DataRetention</c> configuration section, because nobody has set
    /// one. The sweep must still run. Against the pre-fix tree this failed: the absent flag resolved to
    /// false and the 100-day-old notification survived.
    /// </summary>
    [Fact]
    public async Task Enabled_Defaults_On_When_Nothing_Is_Configured()
    {
        await EnsureSchemaAsync();
        var aged = DateTimeOffset.UtcNow.AddDays(-100);

        await using (var seed = NewContext())
        {
            seed.Add(Row(aged));
            await seed.CommitAsync(CancellationToken.None);
        }

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
    /// The sweep over the REAL <see cref="AppConfigurationProvider"/> against this database — no mock stands
    /// between the test and the condition a deployed host is in. That matters: a stubbed provider is what let
    /// the old tests pass while production did nothing. The per-task tuning keys it serves resolve to absent
    /// here, exactly as they do in production, so every task falls through to its RetentionDefaults window.
    /// </summary>
    private DataRetentionBackgroundService NewSweep(CleansiaDbContext ctx, IConfiguration configuration)
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
            new AppConfigurationProvider(ctx),
            new DataRetentionConfig(configuration),
            _blobClientFactory.Object,
            NullLogger<DataRetentionBackgroundService>.Instance);
    }

    private async Task<List<UserNotification>> ReadNotificationsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
