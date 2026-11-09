using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The nightly rollover on Postgres. The store must report the period closed and hold exactly one open
/// period that starts the day after it and runs the configured length, 14 days unless
/// PayPeriodClosing:LengthInDays says otherwise (owner decisions 2026-10-04 and 2026-11-09), as does the
/// period the bootstrap opens when none is open. The close and that successor are durable before the first
/// invoice, so pay calculated while the run is still invoicing lands in the successor.
/// </summary>
[Collection("PostgresCollection")]
public class PayPeriodRolloverTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-rollover";
    private const string CurrencyId = "currency-czk-rollover";
    private const decimal JobPay = 420m;

    private static Task RegisterBatch(IServiceCollection services)
    {
        // The batch is registered by the Functions host, not by AddCoreBindings.
        services.AddScoped<PayPeriodBackgroundService>();
        services.AddScoped<IPayPeriodBackgroundService>(sp => sp.GetRequiredService<PayPeriodBackgroundService>());
        services.Replace(ServiceDescriptor.Singleton<IEmailService>(new Mock<IEmailService>().Object));
        return Task.CompletedTask;
    }

    private static Func<IServiceCollection, Task> RegisterBatch(int? lengthSetting) => async services =>
    {
        await RegisterBatch(services);
        if (lengthSetting is not null)
        {
            services.Replace(ServiceDescriptor.Singleton<IPayPeriodClosingConfig>(new PayPeriodClosingConfig(
                new ConfigurationBuilder()
                    .AddInMemoryCollection([new KeyValuePair<string, string?>("PayPeriodClosing:LengthInDays", lengthSetting.ToString())])
                    .Build())));
        }
    };

    [Theory]
    [InlineData(null, 14)]
    [InlineData(3, 3)]
    public async Task An_Expired_Period_With_Nothing_To_Invoice_Closes_And_Its_Successor_Opens(int? lengthSetting, int days)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = PayPeriod.Create(today.AddDays(-31), today.AddDays(-2));

        await TestMethod(
            setup: RegisterBatch(lengthSetting),
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
                Assert.Equal(expired.EndDate.AddDays(days), successor.EndDate);
                Assert.Equal(TestTenants.Default, successor.TenantId);
            },
            transactional: false);
    }

    [Theory]
    [InlineData(null, 14)]
    [InlineData(3, 3)]
    public async Task With_No_Open_Period_The_Bootstrap_Opens_The_Configured_Length_From_Today(int? lengthSetting, int days)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await TestMethod(
            setup: RegisterBatch(lengthSetting),
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
                Assert.Equal(today.AddDays(days - 1), opened.EndDate);
            },
            transactional: false);
    }

    [Fact]
    public async Task Pay_Calculated_While_The_Close_Is_Invoicing_Lands_In_The_Next_Period()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = PayPeriod.Create(today.AddDays(-31), today.AddDays(-2));
        string employeeId = default!;
        string lateOrderId = default!;
        BusinessResult<CalculateOrderPay.Response>? calculated = null;

        await TestMethod(
            setup: async services =>
            {
                await RegisterBatch(services);
                // The cleaner's invoice e-mail goes out after their invoice commits; the job's pay is
                // calculated right then, as the pay queue would while the run is still invoicing.
                services.Replace(ServiceDescriptor.Scoped<IEmailService>(provider =>
                {
                    var email = new Mock<IEmailService>();
                    email.Setup(e => e.SendPeriodClosedEmailAsync(It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                            It.IsAny<string>(), It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                        .Returns(async () =>
                        {
                            await using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
                            calculated = await scope.ServiceProvider.GetRequiredService<IMediator>()
                                .Send(new CalculateOrderPay.Command(lateOrderId, employeeId));
                            return "message-id";
                        });
                    return email.Object;
                }));
            },
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
                country.Id = CountryId;
                context.Countries.Add(country);
                var currency = Currency.Create("CZK", "Kč", "Czech koruna");
                currency.IsActive = true;
                currency.Id = CurrencyId;
                currency.SetAsDefault(true);
                context.Currencies.Add(currency);

                var user = User.CreateWithPassword(
                    "rollover-cleaner@cleansia.test", "12345678Test!", "Roll", "Over", UserProfile.Employee);
                user.ConfirmEmail();
                context.Users.Add(user);
                var employee = Employee.CreateWithUser(user);
                context.Add(employee);

                var package = Package.Create("Rollover package", "One clean");
                context.Add(package);
                context.Add(EmployeePayConfig.CreateForPackage(package.Id, JobPay, CurrencyId));

                context.Add(expired);
                var worked = CompletedOrder(package, employee, "rollover-worked@cleansia.test");
                var late = CompletedOrder(package, employee, "rollover-late@cleansia.test");
                context.AddRange(worked, late);
                await context.CommitAsync(CancellationToken.None);

                context.Add(OrderEmployeePay.Create(worked.Id, employee.Id, expired.Id, CurrencyId, basePay: JobPay, totalPay: JobPay));
                await context.CommitAsync(CancellationToken.None);

                employeeId = employee.Id;
                lateOrderId = late.Id;
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);
                return true;
            },
            assert: async (context, _) =>
            {
                Assert.True(calculated?.IsSuccess, calculated?.Error?.Message);

                var successor = await context.PayPeriods.IgnoreQueryFilters().SingleAsync(p => p.Status == PayPeriodStatus.Open);
                var latePay = await context.OrderEmployeePays.IgnoreQueryFilters().SingleAsync(p => p.OrderId == lateOrderId);
                Assert.Equal(successor.Id, latePay.PayPeriodId);
                Assert.Null(latePay.EmployeeInvoiceId);

                var invoice = await context.EmployeeInvoices.IgnoreQueryFilters().SingleAsync(i => i.PayPeriodId == expired.Id);
                Assert.Equal((1, JobPay), (invoice.TotalOrders, invoice.TotalAmount));
            },
            transactional: false);
    }

    private static Order CompletedOrder(Package package, Employee employee, string customerEmail)
    {
        var order = Order.Create(
            customerName: "Rollover Customer",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Rollover St 1", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 1200m)]);
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.MarkCompletedAt(DateTime.UtcNow);
        return order;
    }
}
