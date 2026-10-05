using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The nightly rollover on Postgres, for the period the invoicing path never touches: nothing was
/// worked in it, so no invoice commit carries the close before the successor is looked for. The store
/// must still report it closed and hold exactly one open period that starts the day after it and runs
/// 14 days (owner decision 2026-10-04), as does the period the bootstrap opens when none is open.
/// </summary>
[Collection("PostgresCollection")]
public class PayPeriodRolloverTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private static Task RegisterBatch(IServiceCollection services)
    {
        // The batch is registered by the Functions host, not by AddCoreBindings.
        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.Replace(ServiceDescriptor.Singleton<IEmailService>(new Mock<IEmailService>().Object));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task An_Expired_Period_With_Nothing_To_Invoice_Closes_And_Its_Successor_Opens()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = PayPeriod.Create(today.AddDays(-31), today.AddDays(-2));

        await TestMethod(
            setup: RegisterBatch,
            arrange: async context =>
            {
                context.Add(expired);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);
                return true;
            },
            assert: async (context, _) =>
            {
                var periods = await context.PayPeriods.IgnoreQueryFilters().ToListAsync();

                Assert.Equal(PayPeriodStatus.Closed, periods.Single(p => p.Id == expired.Id).Status);
                var successor = Assert.Single(periods, p => p.Status == PayPeriodStatus.Open);
                Assert.Equal(expired.EndDate.AddDays(1), successor.StartDate);
                Assert.Equal(expired.EndDate.AddDays(14), successor.EndDate);
                Assert.Equal(TestTenants.Default, successor.TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task With_No_Open_Period_The_Bootstrap_Opens_Fourteen_Days_From_Today()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await TestMethod(
            setup: RegisterBatch,
            arrange: (CleansiaDbContext _) => Task.CompletedTask,
            act: async provider =>
            {
                await provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .EnsureOpenPeriodAsync(CancellationToken.None);
                return true;
            },
            assert: async (context, _) =>
            {
                var opened = Assert.Single(await context.PayPeriods.IgnoreQueryFilters().ToListAsync());

                Assert.Equal(PayPeriodStatus.Open, opened.Status);
                Assert.Equal(today, opened.StartDate);
                Assert.Equal(today.AddDays(13), opened.EndDate);
            },
            transactional: false);
    }
}
