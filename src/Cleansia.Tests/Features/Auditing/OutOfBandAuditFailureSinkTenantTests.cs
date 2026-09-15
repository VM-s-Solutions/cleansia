using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Auditing;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D7 — the out-of-band failure row is stamped with the operating company of the account it
/// names, read in the sink's own scope past the tenant filter; only a row that names nobody keeps the
/// ambient tenant. An anonymous refusal's ambient tenant is the default market's operator, which is
/// the right answer for an unknown address and the wrong one for a second operator's customer. A real
/// <see cref="CleansiaDbContext"/> over SQLite in-memory, the <c>UserRepositoryTenantLoginLockoutTests</c>
/// arrangement, so the global tenant filter actually runs.
/// </summary>
public sealed class OutOfBandAuditFailureSinkTenantTests : IDisposable
{
    private const string SecondTenant = TestTenants.Second;
    private const string SecondTenantUserId = "user-second-operator";
    private const string DefaultTenantUserId = "user-default-operator";

    private readonly SqliteConnection _connection;

    public OutOfBandAuditFailureSinkTenantTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    private async Task SeedAsync()
    {
        await using (var ctx = NewContext(TestTenants.Default))
        {
            await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
            ctx.Add(Language.Create("en", "English"));
            var defaultUser = User.CreateWithPassword("default@cleansia.test", "Passw0rd!", "Default", "Operator");
            defaultUser.Id = DefaultTenantUserId;
            ctx.Add(defaultUser);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext(SecondTenant))
        {
            var secondUser = User.CreateWithPassword("second@cleansia.test", "Passw0rd!", "Second", "Operator");
            secondUser.Id = SecondTenantUserId;
            ctx.Add(secondUser);
            await ctx.CommitAsync(CancellationToken.None);
        }
    }

    private OutOfBandAuditFailureSink Sink(string? ambientTenant) =>
        new(new SingleDbScopeFactory(_connection), new FixedTenantProvider(ambientTenant), NullLogger<OutOfBandAuditFailureSink>.Instance);

    private static CustomerActionAudit Refusal(string? userId) =>
        CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: "203.0.113.9", deviceLabel: null, deviceId: null,
            action: "customer.session.login", resourceType: "User", resourceId: userId, success: false,
            errorCode: "auth.invalid_credentials", payloadJson: null, correlationId: null);

    private async Task<List<CustomerActionAudit>> RowsAsync()
    {
        await using var ctx = NewContext(tenantId: null);
        return await ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();
    }

    [Fact]
    public async Task A_Row_That_Names_A_Second_Operators_Account_Is_Stamped_With_That_Operator_Not_The_Ambient_Tenant()
    {
        await SeedAsync();

        await Sink(ambientTenant: TestTenants.Default).RecordFailureAsync(Refusal(SecondTenantUserId), CancellationToken.None);

        var row = Assert.Single(await RowsAsync());
        Assert.Equal(SecondTenantUserId, row.UserId);
        Assert.Equal(SecondTenant, row.TenantId);
    }

    [Fact]
    public async Task A_Row_That_Names_Nobody_Keeps_The_Ambient_Tenant()
    {
        await SeedAsync();

        await Sink(ambientTenant: TestTenants.Default).RecordFailureAsync(Refusal(userId: null), CancellationToken.None);

        var row = Assert.Single(await RowsAsync());
        Assert.Null(row.UserId);
        Assert.Equal(TestTenants.Default, row.TenantId);
    }

    [Fact]
    public async Task A_Row_That_Names_An_Account_No_Row_Holds_Keeps_The_Ambient_Tenant()
    {
        await SeedAsync();

        await Sink(ambientTenant: TestTenants.Default).RecordFailureAsync(Refusal("user-gone"), CancellationToken.None);

        var row = Assert.Single(await RowsAsync());
        Assert.Equal("user-gone", row.UserId);
        Assert.Equal(TestTenants.Default, row.TenantId);
    }

    [Fact]
    public async Task A_Row_That_Names_An_Account_Is_Stamped_With_Its_Operator_Even_With_No_Ambient_Tenant()
    {
        await SeedAsync();

        await Sink(ambientTenant: null).RecordFailureAsync(Refusal(SecondTenantUserId), CancellationToken.None);

        var row = Assert.Single(await RowsAsync());
        Assert.Equal(SecondTenant, row.TenantId);
    }

    [Fact]
    public async Task A_Row_That_Names_Nobody_With_No_Ambient_Tenant_Is_Not_Written()
    {
        await SeedAsync();

        await Sink(ambientTenant: null).RecordFailureAsync(Refusal(userId: null), CancellationToken.None);

        Assert.Empty(await RowsAsync());
    }

    [Fact]
    public async Task The_Admin_Row_Keeps_The_Ambient_Tenant()
    {
        await SeedAsync();
        var entry = new AdminActionAudit
        {
            ActorId = "admin-1", ActorProfile = UserProfile.Administrator, Action = "order.refund",
            Success = false, ErrorCode = "order.not_found", OccurredOn = DateTimeOffset.UtcNow
        };

        await Sink(ambientTenant: TestTenants.Default).RecordFailureAsync(entry, CancellationToken.None);

        await using var ctx = NewContext(tenantId: null);
        var row = Assert.Single(await ctx.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(TestTenants.Default, row.TenantId);
    }

    private sealed class SingleDbScopeFactory(SqliteConnection connection) : IServiceScopeFactory, IServiceProvider, IServiceScope
    {
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public void Dispose() { }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(CleansiaDbContext)
                ? new CleansiaDbContext(
                    new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(connection).Options,
                    new TestUserSessionProvider("system", "system@cleansia.test"),
                    new FixedTenantProvider(tenantId: null))
                : null;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
