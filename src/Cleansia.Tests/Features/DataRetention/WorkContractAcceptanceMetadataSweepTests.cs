using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.DataRetention;

/// <summary>
/// ADR-0068 D5: the IP address, device label and device id on a contract-for-work acceptance are
/// blanked by THIS sweep once the row is older than the company's window, and nothing else on the row
/// moves — the act, the seat, the text and the frozen facts are the evidence and stay for as long as the
/// order does. No row is ever deleted. Each company reads its own window, so a one-year window one
/// company set blanks that company's rows alone. Real repositories over SQLite, the
/// <c>DisputeTextRetentionSweepTests</c> shape; the Postgres twin drives the same sweep through the
/// real setting row.
/// </summary>
public sealed class WorkContractAcceptanceMetadataSweepTests : IDisposable
{
    private const string Ip = "198.51.100.7";
    private const string DeviceLabel = "Firefox";
    private const string DeviceId = "device-claim-1";
    private const string Facts = "{\"orderNumber\":\"ORD-1\"}";

    private readonly SqliteConnection _connection;
    private readonly Mock<IAppConfigurationProvider> _configProvider = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public WorkContractAcceptanceMetadataSweepTests()
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
        Assert.Equal("retention.work_contract_metadata.years", RetentionDefaults.WorkContractMetadataRetentionYearsKey);
        Assert.Equal(3, RetentionDefaults.DefaultWorkContractMetadataRetentionYears);
    }

    [Fact]
    public async Task A_Row_Older_Than_The_Window_Loses_The_Trio_And_Keeps_Everything_Else_A_Younger_Row_Is_Untouched()
    {
        await EnsureSchemaAsync();
        var cutoff = DateTimeOffset.UtcNow.AddYears(-RetentionDefaults.DefaultWorkContractMetadataRetentionYears);

        await SeedAsync(
            Row("acc-old", cutoff.AddDays(-1), TestTenants.Default),
            Row("acc-young", cutoff.AddDays(1), TestTenants.Default));

        await RunSweepAsync(EmptyConfiguration());

        var rows = await ReadAllAsync();
        Assert.Equal(2, rows.Count);

        var old = rows.Single(r => r.Id == "acc-old");
        Assert.Null(old.IpAddress);
        Assert.Null(old.DeviceLabel);
        Assert.Null(old.DeviceId);
        Assert.Equal("order-acc-old", old.OrderId);
        Assert.Equal("seat-acc-old", old.OrderEmployeeId);
        Assert.Equal("employee-1", old.EmployeeId);
        Assert.Equal(WorkContractTestData.TextIdEn, old.LegalDocumentTextId);
        Assert.Equal(WorkContractTestData.Version, old.DocumentVersion);
        Assert.Equal(cutoff.AddDays(-1), old.AcceptedOn);
        Assert.Equal(Facts, old.FactsJson);

        var young = rows.Single(r => r.Id == "acc-young");
        Assert.Equal(Ip, young.IpAddress);
        Assert.Equal(DeviceLabel, young.DeviceLabel);
        Assert.Equal(DeviceId, young.DeviceId);
    }

    [Fact]
    public async Task Each_Company_Reads_Its_Own_Window_So_A_Shorter_One_Blanks_That_Companys_Rows_Alone()
    {
        await EnsureSchemaAsync();
        var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2);

        await SeedAsync(
            Row("acc-cz", twoYearsAgo, TestTenants.Default),
            Row("acc-sk", twoYearsAgo, TestTenants.Second),
            Row("acc-sk-young", DateTimeOffset.UtcNow.AddMonths(-6), TestTenants.Second));

        var tenantProvider = new FixedTenantProvider(null);
        _configProvider
            .Setup(c => c.GetTenantSettingAsync(RetentionDefaults.WorkContractMetadataRetentionYearsKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => tenantProvider.GetCurrentTenantId() == TestTenants.Second ? "1" : null);

        await RunSweepAsync(EmptyConfiguration(), tenantProvider);

        var rows = await ReadAllAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(Ip, rows.Single(r => r.Id == "acc-cz").IpAddress);
        Assert.Null(rows.Single(r => r.Id == "acc-sk").IpAddress);
        Assert.Null(rows.Single(r => r.Id == "acc-sk").DeviceId);
        Assert.Equal(Ip, rows.Single(r => r.Id == "acc-sk-young").IpAddress);
    }

    [Fact]
    public async Task A_Backlog_Larger_Than_One_Batch_Is_Drained_In_One_Run_And_No_Row_Is_Deleted()
    {
        await EnsureSchemaAsync();
        var past = DateTimeOffset.UtcNow.AddYears(-RetentionDefaults.DefaultWorkContractMetadataRetentionYears).AddDays(-1);
        var expired = RetentionDefaults.BatchSize * 2 + 1;

        await SeedAsync(Enumerable.Range(0, expired)
            .Select(i => Row($"acc-{i}", past.AddMinutes(-i), TestTenants.Default))
            .Append(Row("acc-young", DateTimeOffset.UtcNow.AddDays(-1), TestTenants.Default))
            .ToArray());

        await RunSweepAsync(EmptyConfiguration());

        var rows = await ReadAllAsync();
        Assert.Equal(expired + 1, rows.Count);
        var withTrio = Assert.Single(rows, r => r.IpAddress != null);
        Assert.Equal("acc-young", withTrio.Id);
    }

    [Fact]
    public async Task The_Master_Switch_Off_Blanks_Nothing()
    {
        await EnsureSchemaAsync();

        await SeedAsync(Row("acc-old", DateTimeOffset.UtcNow.AddYears(-10), TestTenants.Default));

        await RunSweepAsync(ConfigurationFrom(("DataRetention:Enabled", "false")));

        Assert.Equal(Ip, Assert.Single(await ReadAllAsync()).IpAddress);
    }

    private static WorkContractAcceptance Row(string id, DateTimeOffset acceptedOn, string tenantId)
    {
        var row = WorkContractAcceptance.Create(
            $"order-{id}", $"seat-{id}", "employee-1", WorkContractTestData.Document().TextFor("en")!,
            WorkContractTestData.Version, "cleansia.partner", Ip, DeviceLabel, DeviceId, Facts);
        row.Id = id;
        row.TenantId = tenantId;
        typeof(WorkContractAcceptance).GetProperty(nameof(WorkContractAcceptance.AcceptedOn))!.SetValue(row, acceptedOn);
        return row;
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

    private async Task SeedAsync(params WorkContractAcceptance[] rows)
    {
        await using var seed = NewContext();
        seed.WorkContractAcceptances.AddRange(rows);
        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task RunSweepAsync(IConfiguration configuration, FixedTenantProvider? tenantProvider = null)
    {
        tenantProvider ??= new FixedTenantProvider(null);
        await using var ctx = NewContext(tenantProvider);
        await NewSweep(ctx, tenantProvider, configuration).RunAllRetentionTasksAsync(CancellationToken.None);
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
            new WorkContractAcceptanceRepository(ctx),
            new AddressRepository(ctx),
            new OrderPhotoRepository(ctx),
            new AdminActionAuditRepository(ctx),
            new EmployeeActionAuditRepository(ctx, Mock.Of<IServiceScopeFactory>()),
            new GuestOrderAccessTokenRepository(ctx),
            new TenantRepository(ctx),
            tenantProvider,
            _configProvider.Object,
            new DataRetentionConfig(configuration),
            _blobClientFactory.Object,
            new ArchiveWriteGate(),
            NullLogger<DataRetentionBackgroundService>.Instance);
    }

    private async Task<List<WorkContractAcceptance>> ReadAllAsync()
    {
        await using var ctx = NewContext();
        return await ctx.WorkContractAcceptances.IgnoreQueryFilters().ToListAsync();
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
