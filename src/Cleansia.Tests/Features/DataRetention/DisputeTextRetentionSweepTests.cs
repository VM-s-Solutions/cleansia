using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
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
/// Owner ruling 2026-09-14: the erasure leaves a dispute's text in place under a
/// <c>TextRetainedUntil</c> stamp, and it is THIS sweep that blanks it — the description, the resolution
/// notes and every message — once the stamp is past, clearing the stamp so the row is not re-read on
/// every tick. A dispute whose window is still open keeps its text; a dispute never stamped (its
/// customer was never erased) is never touched, however old; the master switch stops it; and a backlog
/// wider than one batch drains in one run. Real repositories over SQLite, the
/// <c>CustomerActionAuditRetentionTests</c> shape — the assertions are on rows, never on a mock.
/// </summary>
public sealed class DisputeTextRetentionSweepTests : IDisposable
{
    private const string Description = "The kitchen floor was not mopped.";
    private const string Message = "Photos attached.";

    private readonly SqliteConnection _connection;
    private readonly Mock<IAppConfigurationProvider> _configProvider = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public DisputeTextRetentionSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _configProvider
            .Setup(c => c.GetTenantSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void The_Window_Defaults_To_Three_Years_Under_The_Contracted_Key()
    {
        Assert.Equal("retention.dispute_text.years", RetentionDefaults.DisputeTextRetentionYearsKey);
        Assert.Equal(3, RetentionDefaults.DefaultDisputeTextRetentionYears);
    }

    [Fact]
    public async Task A_Dispute_Whose_Stamp_Is_Past_Is_Blanked_And_The_Stamp_Cleared()
    {
        await EnsureSchemaAsync();
        var now = DateTimeOffset.UtcNow;

        await SeedAsync(Stamped("dispute-past", now.AddDays(-1)));

        await RunSweepAsync(EmptyConfiguration());

        var dispute = await ReadAsync("dispute-past");
        Assert.Equal(AnonymizationMarker.Value, dispute.Description);
        Assert.Equal(AnonymizationMarker.Value, dispute.ResolutionNotes);
        Assert.All(dispute.Messages, m => Assert.Equal(AnonymizationMarker.Value, m.Message));
        Assert.Null(dispute.TextRetainedUntil);
    }

    [Fact]
    public async Task A_Dispute_Whose_Window_Is_Still_Open_Keeps_Its_Text_And_Its_Stamp()
    {
        await EnsureSchemaAsync();
        var retainedUntil = DateTimeOffset.UtcNow.AddDays(1);

        await SeedAsync(Stamped("dispute-open", retainedUntil));

        await RunSweepAsync(EmptyConfiguration());

        var dispute = await ReadAsync("dispute-open");
        Assert.Equal(Description, dispute.Description);
        Assert.All(dispute.Messages, m => Assert.Equal(Message, m.Message));
        Assert.Equal(retainedUntil, dispute.TextRetainedUntil);
    }

    [Fact]
    public async Task A_Dispute_That_Was_Never_Stamped_Is_Never_Touched_However_Old()
    {
        await EnsureSchemaAsync();

        var ancient = Unstamped("dispute-unstamped");
        ancient.Created("user-1", DateTimeOffset.UtcNow.AddYears(-10));
        await SeedAsync(ancient);

        await RunSweepAsync(EmptyConfiguration());

        var dispute = await ReadAsync("dispute-unstamped");
        Assert.Equal(Description, dispute.Description);
        Assert.All(dispute.Messages, m => Assert.Equal(Message, m.Message));
        Assert.Null(dispute.TextRetainedUntil);
    }

    [Fact]
    public async Task The_Master_Switch_Off_Blanks_Nothing()
    {
        await EnsureSchemaAsync();

        await SeedAsync(Stamped("dispute-past", DateTimeOffset.UtcNow.AddDays(-1)));

        await RunSweepAsync(ConfigurationFrom(("DataRetention:Enabled", "false")));

        var dispute = await ReadAsync("dispute-past");
        Assert.Equal(Description, dispute.Description);
        Assert.NotNull(dispute.TextRetainedUntil);
    }

    [Fact]
    public async Task A_Backlog_Larger_Than_One_Batch_Is_Drained_In_One_Run()
    {
        await EnsureSchemaAsync();
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        var expired = RetentionDefaults.BatchSize * 2 + 1;

        await SeedAsync(Enumerable.Range(0, expired)
            .Select(i => Stamped($"dispute-{i}", past.AddMinutes(-i)))
            .Append(Stamped("dispute-open", DateTimeOffset.UtcNow.AddDays(1)))
            .ToArray());

        await RunSweepAsync(EmptyConfiguration());

        await using var ctx = NewContext();
        var stillStamped = await ctx.Disputes.IgnoreQueryFilters().Where(d => d.TextRetainedUntil != null).ToListAsync();
        var survivor = Assert.Single(stillStamped);
        Assert.Equal("dispute-open", survivor.Id);
        Assert.Equal(expired, await ctx.Disputes.IgnoreQueryFilters().CountAsync(d => d.Description == AnonymizationMarker.Value));
    }

    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationFrom(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private CleansiaDbContext NewContext(ITenantProvider? tenantProvider = null) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            tenantProvider ?? new FixedTenantProvider(TestTenants.Default));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
    }

    private async Task SeedAsync(params Dispute[] disputes)
    {
        await using var seed = NewContext();
        seed.Disputes.AddRange(disputes);
        await seed.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// The sweep and its context share one tenant provider, the way one DI scope does on the host: the
    /// per-company override the sweep sets is what the filter and the commit read.
    /// </summary>
    private async Task RunSweepAsync(IConfiguration configuration)
    {
        var tenantProvider = new FixedTenantProvider(null);
        await using var ctx = NewContext(tenantProvider);
        await NewSweep(ctx, tenantProvider, configuration).RunAllRetentionTasksAsync(CancellationToken.None);
    }

    private static Dispute Stamped(string id, DateTimeOffset retainedUntil)
    {
        var dispute = Unstamped(id);
        dispute.Resolve("admin-1", 100m, "Refunded in part.");
        dispute.RetainTextUntil(retainedUntil);
        return dispute;
    }

    private static Dispute Unstamped(string id)
    {
        var dispute = new Dispute($"order-{id}", "user-1", DisputeReason.QualityIssue, Description, "user-1");
        dispute.Id = id;
        dispute.AddMessage(Message, "user-1", isStaff: false);
        dispute.AddMessage(Message, "admin-1", isStaff: true);
        return dispute;
    }

    private DataRetentionBackgroundService NewSweep(CleansiaDbContext ctx, ITenantProvider tenantProvider, IConfiguration configuration)
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
            new CustomerActionAuditRepository(ctx),
            new DisputeRepository(ctx),
            new TenantRepository(ctx),
            tenantProvider,
            _configProvider.Object,
            new DataRetentionConfig(configuration),
            _blobClientFactory.Object,
            new ArchiveWriteGate(),
            NullLogger<DataRetentionBackgroundService>.Instance);
    }

    private async Task<Dispute> ReadAsync(string id)
    {
        await using var ctx = NewContext();
        return await ctx.Disputes.IgnoreQueryFilters().Include(d => d.Messages).SingleAsync(d => d.Id == id);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
