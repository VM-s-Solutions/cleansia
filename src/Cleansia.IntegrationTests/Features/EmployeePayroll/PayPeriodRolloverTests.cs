using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The nightly rollover on Postgres, for the period the invoicing path never touches: nothing was
/// worked in it, so no invoice commit carries the close before the successor is looked for. The store
/// must still report it closed and hold exactly one open period that starts the day after it.
/// </summary>
[Collection("PostgresCollection")]
public class PayPeriodRolloverTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task An_Expired_Period_With_Nothing_To_Invoice_Closes_And_Its_Successor_Opens()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = PayPeriod.Create(today.AddDays(-31), today.AddDays(-2));

        await TestMethod(
            setup: services =>
            {
                // The batch is registered by the Functions host, not by AddCoreBindings.
                services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
                services.Replace(ServiceDescriptor.Singleton<IEmailService>(new Mock<IEmailService>().Object));
                return Task.CompletedTask;
            },
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
                Assert.Equal(TestTenants.Default, successor.TenantId);
            },
            transactional: false);
    }
}
