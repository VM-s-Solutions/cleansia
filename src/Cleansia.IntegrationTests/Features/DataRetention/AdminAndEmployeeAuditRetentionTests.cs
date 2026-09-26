using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// The administrator and cleaner audit tables are kept three years (owner ruling 2026-09-22), swept the way
/// the customer audit is — per row by its own age, per company, through the REAL sweep on real Postgres. The
/// admin row ages by <c>OccurredOn</c>; the cleaner row, which has no such column, by <c>CreatedOn</c>, the
/// moment of the act. Each table reads its own key, so a company shortening one window leaves the other two
/// tables alone.
/// </summary>
[Collection("PostgresCollection")]
public class AdminAndEmployeeAuditRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private static Task WithTheSweep(IServiceCollection services)
    {
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Rows_Past_Three_Years_Go_And_Younger_Rows_Stay_In_Both_Tables_Across_Both_Companies()
    {
        var cutoff = DateTimeOffset.UtcNow.AddYears(-3);
        var backlog = RetentionDefaults.BatchSize + 1;

        await TestMethod(
            setup: WithTheSweep,
            arrange: async context =>
            {
                context.AdminActionAudits.AddRange(
                    AdminRow("ADMIN-OLD-CZ", cutoff.AddDays(-1), TestTenants.Default),
                    AdminRow("ADMIN-OLD-SK", cutoff.AddDays(-1), TestTenants.Second),
                    AdminRow("ADMIN-YOUNG", cutoff.AddDays(1), TestTenants.Default));
                context.EmployeeActionAudits.AddRange(Enumerable.Range(0, backlog)
                    .Select(i => EmployeeRow($"EMP-OLD-{i}", cutoff.AddDays(-1 - i), TestTenants.Default)));
                context.EmployeeActionAudits.AddRange(
                    EmployeeRow("EMP-OLD-SK", cutoff.AddDays(-1), TestTenants.Second),
                    EmployeeRow("EMP-YOUNG", cutoff.AddDays(1), TestTenants.Default));
                await Task.CompletedTask;
            },
            act: RunSweepAsync,
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var admin = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("ADMIN-YOUNG", admin.ResourceId);

                var employee = Assert.Single(await context.EmployeeActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("EMP-YOUNG", employee.OrderId);
            });
    }

    [Fact]
    public async Task Each_Table_Reads_Its_Own_Key_Per_Company()
    {
        var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2);

        await TestMethod(
            setup: WithTheSweep,
            arrange: async context =>
            {
                context.TenantConfigurations.AddRange(
                    Setting(RetentionDefaults.AdminAuditRetentionYearsKey, "1", TestTenants.Default),
                    Setting(RetentionDefaults.EmployeeAuditRetentionYearsKey, "1", TestTenants.Second));

                context.AdminActionAudits.AddRange(
                    AdminRow("ADMIN-CZ", twoYearsAgo, TestTenants.Default),
                    AdminRow("ADMIN-SK", twoYearsAgo, TestTenants.Second));
                context.EmployeeActionAudits.AddRange(
                    EmployeeRow("EMP-CZ", twoYearsAgo, TestTenants.Default),
                    EmployeeRow("EMP-SK", twoYearsAgo, TestTenants.Second));
                context.CustomerActionAudits.Add(CustomerRow("CUST-CZ", twoYearsAgo, TestTenants.Default));
                await Task.CompletedTask;
            },
            act: RunSweepAsync,
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var admin = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("ADMIN-SK", admin.ResourceId);

                var employee = Assert.Single(await context.EmployeeActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("EMP-CZ", employee.OrderId);

                Assert.Single(await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
            });
    }

    private static async Task<bool> RunSweepAsync(IServiceProvider provider)
    {
        await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
        return true;
    }

    private static AdminActionAudit AdminRow(string resourceId, DateTimeOffset occurredOn, string tenantId) =>
        new()
        {
            ActorId = "admin-1", Action = "order.refund", ActorProfile = UserProfile.Administrator, Success = true,
            ResourceType = "Order", ResourceId = resourceId, OccurredOn = occurredOn, TenantId = tenantId,
        };

    private static EmployeeActionAudit EmployeeRow(string orderId, DateTimeOffset createdOn, string tenantId)
    {
        var row = EmployeeActionAudit.Create("employee-1", orderId, EmployeeAuditAction.OrderDropped);
        row.TenantId = tenantId;
        row.Created("employee-1", createdOn);
        return row;
    }

    private static CustomerActionAudit CustomerRow(string resourceId, DateTimeOffset occurredOn, string tenantId)
    {
        var row = CustomerActionAudit.Create(
            userId: "cust-1", clientAudience: JwtAudiences.Customer, ipAddress: null, deviceLabel: null, deviceId: null,
            action: "customer.test.act", resourceType: "Order", resourceId: resourceId, success: true, errorCode: null,
            payloadJson: null, correlationId: null);
        row.TenantId = tenantId;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }

    private static TenantConfiguration Setting(string key, string value, string tenantId)
    {
        var setting = TenantConfiguration.Create(key, value);
        setting.TenantId = tenantId;
        setting.Created("admin", DateTimeOffset.UtcNow);
        return setting;
    }
}
