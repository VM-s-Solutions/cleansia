using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
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
/// ADR-0062 D5 (Verification #7) — a customer audit row expires three years after ITS OWN act. The
/// window is per row, never "three years after the customer's last act": that anchor kept an active
/// customer's IP addresses for the life of the account (panel C7). So a row at cutoff - 1 day goes and
/// a row at cutoff + 1 day stays for the SAME user; a guest row (<c>UserId</c> null) expires the same
/// way; the admin and employee tables are never touched (ADR-0012 D6 stands); the tenant setting
/// widens the window; the master switch stops it; and a backlog larger than one batch is drained in
/// one run. Real repositories over SQLite, the <c>UserNotificationRetentionAndGdprTests</c> shape —
/// the assertions are on rows, never on a mock verification.
/// </summary>
public sealed class CustomerActionAuditRetentionTests : IDisposable
{
    private const string UserId = "user-audit-ret-1";
    private const int DefaultYears = RetentionDefaults.DefaultCustomerAuditRetentionYears;

    private readonly SqliteConnection _connection;
    private readonly Mock<IAppConfigurationProvider> _configProvider = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public CustomerActionAuditRetentionTests()
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
        Assert.Equal("retention.customer_audit.years", RetentionDefaults.CustomerAuditRetentionYearsKey);
        Assert.Equal(3, DefaultYears);
    }

    [Fact]
    public async Task A_Row_Older_Than_The_Window_Goes_And_A_Younger_Row_Of_The_Same_User_Stays()
    {
        await EnsureSchemaAsync();
        var cutoff = DateTimeOffset.UtcNow.AddYears(-DefaultYears);

        await SeedAsync(
            Row(cutoff.AddDays(-1), UserId, "ORD-OLD"),
            Row(cutoff.AddDays(1), UserId, "ORD-YOUNG"));

        await RunSweepAsync(EmptyConfiguration());

        var survivor = Assert.Single(await ReadRowsAsync());
        Assert.Equal("ORD-YOUNG", survivor.ResourceId);
        Assert.Equal(UserId, survivor.UserId);
    }

    [Fact]
    public async Task A_Guest_Row_Older_Than_The_Window_Goes()
    {
        await EnsureSchemaAsync();
        var cutoff = DateTimeOffset.UtcNow.AddYears(-DefaultYears);

        await SeedAsync(
            Row(cutoff.AddDays(-1), userId: null, "ORD-GUEST-OLD"),
            Row(cutoff.AddDays(1), userId: null, "ORD-GUEST-YOUNG"));

        await RunSweepAsync(EmptyConfiguration());

        var survivor = Assert.Single(await ReadRowsAsync());
        Assert.Equal("ORD-GUEST-YOUNG", survivor.ResourceId);
        Assert.Null(survivor.UserId);
    }

    [Fact]
    public async Task The_Tenant_Setting_Widens_The_Window()
    {
        await EnsureSchemaAsync();
        _configProvider
            .Setup(c => c.GetTenantSettingAsync(RetentionDefaults.CustomerAuditRetentionYearsKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("5");
        var now = DateTimeOffset.UtcNow;

        await SeedAsync(
            Row(now.AddYears(-4), UserId, "ORD-FOUR-YEARS"),
            Row(now.AddYears(-6), UserId, "ORD-SIX-YEARS"));

        await RunSweepAsync(EmptyConfiguration());

        var survivor = Assert.Single(await ReadRowsAsync());
        Assert.Equal("ORD-FOUR-YEARS", survivor.ResourceId);
    }

    [Fact]
    public async Task The_Admin_And_Employee_Tables_Are_Never_Touched()
    {
        await EnsureSchemaAsync();
        var cutoff = DateTimeOffset.UtcNow.AddYears(-DefaultYears);

        await using (var seed = NewContext())
        {
            seed.AdminActionAudits.Add(new AdminActionAudit
            {
                ActorId = "admin-1", ActorProfile = UserProfile.Administrator, Action = "order.refund",
                Success = true, OccurredOn = cutoff.AddYears(-1), TenantId = TestTenants.Default
            });
            var employeeRow = EmployeeActionAudit.Create("employee-1", "ORD-1", EmployeeAuditAction.OrderDropped);
            employeeRow.TenantId = TestTenants.Default;
            employeeRow.Created("employee-1", cutoff.AddYears(-1));
            seed.EmployeeActionAudits.Add(employeeRow);
            seed.CustomerActionAudits.Add(Row(cutoff.AddDays(-1), UserId, "ORD-OLD"));
            await seed.CommitAsync(CancellationToken.None);
        }

        await RunSweepAsync(EmptyConfiguration());

        await using var ctx = NewContext();
        Assert.Empty(await ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(1, await ctx.AdminActionAudits.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await ctx.EmployeeActionAudits.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task The_Master_Switch_Off_Deletes_Nothing()
    {
        await EnsureSchemaAsync();
        var cutoff = DateTimeOffset.UtcNow.AddYears(-DefaultYears);

        await SeedAsync(Row(cutoff.AddDays(-1), UserId, "ORD-OLD"));

        await RunSweepAsync(ConfigurationFrom(("DataRetention:Enabled", "false")));

        Assert.Single(await ReadRowsAsync());
    }

    [Fact]
    public async Task A_Backlog_Larger_Than_One_Batch_Is_Drained_In_One_Run()
    {
        await EnsureSchemaAsync();
        var cutoff = DateTimeOffset.UtcNow.AddYears(-DefaultYears);
        var expired = RetentionDefaults.BatchSize * 2 + 1;

        await SeedAsync(Enumerable.Range(0, expired)
            .Select(i => Row(cutoff.AddDays(-1 - i), UserId, $"ORD-{i}"))
            .Append(Row(cutoff.AddDays(1), UserId, "ORD-YOUNG"))
            .ToArray());

        await RunSweepAsync(EmptyConfiguration());

        var survivor = Assert.Single(await ReadRowsAsync());
        Assert.Equal("ORD-YOUNG", survivor.ResourceId);
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
            new FixedTenantProvider(TestTenants.Default));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private async Task SeedAsync(params CustomerActionAudit[] rows)
    {
        await using var seed = NewContext();
        seed.CustomerActionAudits.AddRange(rows);
        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task RunSweepAsync(IConfiguration configuration)
    {
        await using var ctx = NewContext();
        await NewSweep(ctx, configuration).RunAllRetentionTasksAsync(CancellationToken.None);
    }

    private static CustomerActionAudit Row(DateTimeOffset occurredOn, string? userId, string resourceId)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: null, deviceLabel: null, deviceId: null,
            action: "customer.test.act", resourceType: "Order", resourceId: resourceId, success: true, errorCode: null,
            payloadJson: null, correlationId: null);
        row.TenantId = TestTenants.Default;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }

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
            new CustomerActionAuditRepository(ctx),
            _configProvider.Object,
            new DataRetentionConfig(configuration),
            _blobClientFactory.Object,
            NullLogger<DataRetentionBackgroundService>.Instance);
    }

    private async Task<List<CustomerActionAudit>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
