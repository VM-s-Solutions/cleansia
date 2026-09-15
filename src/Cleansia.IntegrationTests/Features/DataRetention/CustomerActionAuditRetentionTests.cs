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
/// ADR-0062 D5 (Verification #7) through the REAL sweep on real Postgres — the whole
/// <c>RunAllRetentionTasksAsync</c>, not the repository method alone, so what is proven is the wiring the
/// Functions timer runs: the customer audit task is registered, reads its window, and deletes per row by
/// that row's own <c>OccurredOn</c> across both operating companies. A backlog wider than one batch is
/// drained in the one run, which on Postgres exercises the id-list delete the batching is built on.
/// Rows the sweep must never reach — a younger row of the same user, the admin table, the employee
/// table — are counted after it. The second case is the per-company read: a window one company set
/// shortens that company alone.
/// </summary>
[Collection("PostgresCollection")]
public class CustomerActionAuditRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string UserId = "user-audit-ret-pg";

    [Fact]
    public async Task The_Sweep_Deletes_Rows_By_Their_Own_Age_Across_Tenants_And_Nothing_Else()
    {
        var cutoff = DateTimeOffset.UtcNow.AddYears(-RetentionDefaults.DefaultCustomerAuditRetentionYears);
        var backlog = RetentionDefaults.BatchSize + 1;

        await TestMethod(
            setup: services =>
            {
                services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
                services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                context.CustomerActionAudits.AddRange(
                    Row(cutoff.AddDays(1), UserId, "ORD-YOUNG", TestTenants.Default),
                    Row(cutoff.AddDays(-1), null, "ORD-GUEST", TestTenants.Default),
                    Row(cutoff.AddDays(-1), "cust-sk", "ORD-SK", TestTenants.Second));
                context.CustomerActionAudits.AddRange(Enumerable.Range(0, backlog)
                    .Select(i => Row(cutoff.AddDays(-1 - i), UserId, $"ORD-OLD-{i}", TestTenants.Default)));

                context.AdminActionAudits.Add(new AdminActionAudit
                {
                    ActorId = "admin-1", Action = "order.refund", ActorProfile = UserProfile.Administrator,
                    Success = true, OccurredOn = cutoff.AddYears(-1), TenantId = TestTenants.Default
                });
                var employeeRow = EmployeeActionAudit.Create("employee-1", "ORD-OLD-0", EmployeeAuditAction.OrderDropped);
                employeeRow.TenantId = TestTenants.Default;
                employeeRow.Created("employee-1", cutoff.AddYears(-1));
                context.EmployeeActionAudits.Add(employeeRow);
                await Task.CompletedTask;
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var remaining = await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();
                var survivor = Assert.Single(remaining);
                Assert.Equal("ORD-YOUNG", survivor.ResourceId);
                Assert.Equal(UserId, survivor.UserId);

                Assert.Equal(1, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
                Assert.Equal(1, await context.EmployeeActionAudits.IgnoreQueryFilters().CountAsync());
            });
    }

    /// <summary>
    /// Each operating company keeps its own window. A row under <c>retention.customer_audit.years</c>
    /// for the second company alone shortens ITS window to a year: its two-year-old rows go, the first
    /// company's two-year-old rows stay under the three-year default, and the setting row itself — a
    /// stamped row of the second company — is untouched by the sweep.
    /// </summary>
    [Fact]
    public async Task A_Companys_Own_Window_Is_Read_Per_Company_So_Its_Rows_Go_And_The_Other_Companys_Stay()
    {
        var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2);

        await TestMethod(
            setup: services =>
            {
                services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
                services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                var window = TenantConfiguration.Create(RetentionDefaults.CustomerAuditRetentionYearsKey, "1");
                window.TenantId = TestTenants.Second;
                window.Created("admin-sk", DateTimeOffset.UtcNow);
                context.TenantConfigurations.Add(window);

                context.CustomerActionAudits.AddRange(
                    Row(twoYearsAgo, "cust-cz", "ORD-CZ-TWO-YEARS", TestTenants.Default),
                    Row(twoYearsAgo, "cust-sk", "ORD-SK-TWO-YEARS", TestTenants.Second),
                    Row(DateTimeOffset.UtcNow.AddMonths(-6), "cust-sk", "ORD-SK-SIX-MONTHS", TestTenants.Second));
                await Task.CompletedTask;
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var remaining = await context.CustomerActionAudits.IgnoreQueryFilters()
                    .Select(a => a.ResourceId)
                    .ToListAsync();
                Assert.Equal(["ORD-CZ-TWO-YEARS", "ORD-SK-SIX-MONTHS"], remaining.OrderBy(r => r, StringComparer.Ordinal));

                var setting = Assert.Single(await context.TenantConfigurations.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(TestTenants.Second, setting.TenantId);
                Assert.Equal("1", setting.Value);
            });
    }

    private static CustomerActionAudit Row(DateTimeOffset occurredOn, string? userId, string resourceId, string tenantId)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: null, deviceLabel: null, deviceId: null,
            action: "customer.test.act", resourceType: "Order", resourceId: resourceId, success: true, errorCode: null,
            payloadJson: null, correlationId: null);
        row.TenantId = tenantId;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }
}
