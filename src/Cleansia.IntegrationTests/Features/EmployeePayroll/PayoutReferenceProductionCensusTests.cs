using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The census that discharges the twelve hand-authored fixture symbols: every PRODUCTION path that
/// constructs an <see cref="EmployeeInvoice"/> yields a non-null symbol matching <c>^[1-9][0-9]{9}$</c>,
/// asserted through the real MediatR pipeline and the real background service rather than through a
/// literal a test wrote. Without this, twelve fixtures pin a shape production never produces — the
/// exact anti-pattern that let the field ship null on every row.
/// </summary>
[Collection("PostgresCollection")]
public class PayoutReferenceProductionCensusTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string ProducedSymbolPattern = "^[1-9][0-9]{9}$";

    private const string CountryId = "country-cz-census";
    private const string CurrencyId = "currency-czk-census";

    private static readonly List<string> EmployeeIds = [];
    private static string _payPeriodId = default!;

    [Fact]
    public async Task The_Admin_Command_Path_Stamps_A_Produced_Symbol()
    {
        await TestMethod(
            arrange: context => SeedEmployeesWithUnpaidPays(context, count: 1),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GenerateInvoice.Command(EmployeeIds[0], _payPeriodId)),
            assert: async (CleansiaDbContext context, BusinessResult<GenerateInvoice.Response> result) =>
            {
                Assert.True(result.IsSuccess);

                var invoice = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .SingleAsync(i => i.Id == result.Value!.InvoiceId);

                Assert.NotNull(invoice.VariableSymbol);
                Assert.Matches(ProducedSymbolPattern, invoice.VariableSymbol!);
            },
            // NON-transactional, and that is the point rather than a harness convenience: the allocator
            // must never run inside an explicit transaction (ADR-0046 §D2.1), so a harness that wraps
            // the act in one would be testing a configuration production forbids.
            transactional: false);
    }

    [Fact]
    public async Task The_Pay_Period_Batch_Path_Stamps_A_Produced_Symbol()
    {
        await TestMethod(
            setup: services =>
            {
                // The batch is registered by the Functions host, not by AddCoreBindings.
                services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
                services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => new SilentEmailService()));
                return Task.CompletedTask;
            },
            arrange: context => SeedEmployeesWithUnpaidPays(context, count: 1, closedPeriod: true),
            act: async provider =>
            {
                await provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var invoices = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Where(i => i.PayPeriodId == _payPeriodId)
                    .ToListAsync();

                Assert.NotEmpty(invoices);
                Assert.All(invoices, i =>
                {
                    Assert.NotNull(i.VariableSymbol);
                    Assert.Matches(ProducedSymbolPattern, i.VariableSymbol!);
                });
            },
            transactional: false);
    }

    /// <summary>
    /// The test the pre-ADR design cannot pass: N invoices created concurrently inside ONE pay period
    /// must carry N DISTINCT symbols. The old generator distinguished cleaners by 10 000 hash buckets
    /// within a period — ~39 % chance of at least one collision at 100 cleaners — and no test attempted
    /// it, so the failure was invisible until two cleaners shared a bank reference.
    /// </summary>
    [Fact]
    public async Task N_Concurrent_Invoice_Creations_In_One_Period_Yield_N_Distinct_Symbols()
    {
        const int concurrency = 12;

        await TestMethod(
            arrange: context => SeedEmployeesWithUnpaidPays(context, concurrency),
            act: async provider =>
            {
                var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

                var tasks = EmployeeIds.Select(async employeeId =>
                {
                    using var scope = scopeFactory.CreateScope();
                    return await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new GenerateInvoice.Command(employeeId, _payPeriodId));
                }).ToArray();

                // Zero exceptions is half the assertion — Task.WhenAll rethrows the first one.
                return await Task.WhenAll(tasks);
            },
            assert: async (CleansiaDbContext context, BusinessResult<GenerateInvoice.Response>[] results) =>
            {
                Assert.All(results, r => Assert.True(r.IsSuccess));

                var symbols = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Where(i => i.PayPeriodId == _payPeriodId)
                    .Select(i => i.VariableSymbol)
                    .ToListAsync();

                Assert.Equal(concurrency, symbols.Count);
                Assert.Equal(concurrency, symbols.Distinct().Count());
                Assert.All(symbols, s => Assert.Matches(ProducedSymbolPattern, s!));
            },
            transactional: false);
    }

    /// <summary>
    /// The backstop for the one route a duplicate can still arrive by — a hand-written INSERT or a
    /// counter row restored behind the table. It must be a refusal the admin can act on, not the
    /// post-commit <see cref="DbUpdateException"/> the pipeline would otherwise raise as a 500.
    /// </summary>
    [Fact]
    public async Task A_Duplicate_Symbol_Is_A_Business_Failure_Not_A_DbUpdateException()
    {
        await TestMethod(
            arrange: async context =>
            {
                await SeedEmployeesWithUnpaidPays(context, count: 1);

                // Park the symbol the allocator is about to produce on an unrelated invoice, so the
                // insert loses to the unique index exactly as a restored counter row would make it.
                var squatter = EmployeeInvoice.CreateFromOrderPays(
                    EmployeeIds[0],
                    await ForeignPeriodIdAsync(context),
                    [],
                    CurrencyId,
                    $"{DateTime.UtcNow.Year:D4}000001");
                context.Add(squatter);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GenerateInvoice.Command(EmployeeIds[0], _payPeriodId)),
            assert: async (CleansiaDbContext context, BusinessResult<GenerateInvoice.Response> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.InvoiceReferenceUnavailable, result.Error!.Message);

                var created = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.PayPeriodId == _payPeriodId);
                Assert.False(created);
            },
            transactional: false);
    }

    /// <summary>Keeps the batch off the network; every other leg of the run stays real.</summary>
    private sealed class SilentEmailService : IEmailService
    {
        private static Task<string> Sent() => Task.FromResult("test-message-id");

        public Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, string languageCode = Constants.Language.English, CancellationToken ct = default) => Sent();
        public Task<string> SendOrderReceiptEmailAsync(string email, Order order, byte[]? pdfBytes = null, string fileName = "receipt.pdf", string languageCode = Constants.Language.English, CancellationToken ct = default) => Sent();
        public Task<string> SendTestOrderReceiptEmailAsync(string email, string customerName, string orderNumber, string orderDate, string totalAmount, string languageCode = Constants.Language.English, CancellationToken ct = default) => Sent();
        public Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, string languageCode, CancellationToken ct = default) => Sent();
        public Task<string> SendPeriodClosedEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, DateTime closedAt, string periodLabel, string languageCode = Constants.Language.English, byte[]? invoicePdfBytes = null, string? invoiceFileName = null, CancellationToken ct = default) => Sent();
        public Task<string> SendPeriodEndReminderEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, int daysRemaining, string periodLabel, string languageCode = Constants.Language.English, CancellationToken ct = default) => Sent();
        public Task<string> SendOrderStatusUpdateEmailAsync(string email, Order order, string newStatus, string languageCode = Constants.Language.English, CancellationToken ct = default) => Sent();
    }

    private static async Task<string> ForeignPeriodIdAsync(CleansiaDbContext context)
    {
        var period = PayPeriod.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 15));
        context.Add(period);
        await context.CommitAsync(CancellationToken.None);
        return period.Id;
    }

    private static async Task SeedEmployeesWithUnpaidPays(
        CleansiaDbContext context, int count, bool closedPeriod = false)
    {
        EmployeeIds.Clear();

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);

        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        // An EXPIRED window, so the batch's own "close what is due" selector picks it up.
        var payPeriod = closedPeriod
            ? PayPeriod.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)))
            : PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        context.Add(payPeriod);

        var employees = new List<Employee>();
        var orders = new List<Order>();

        for (var i = 0; i < count; i++)
        {
            var user = User.CreateWithPassword(
                $"census-emp-{i}@cleansia.test", "12345678Test!", "Emp", $"Loyee{i}", UserProfile.Employee);
            user.ConfirmEmail();
            context.Users.Add(user);

            var employee = Employee.CreateWithUser(user);
            context.Add(employee);
            employees.Add(employee);

            var order = NewOrder(user.Id, $"census-buyer-{i}@cleansia.test");
            context.Add(order);
            orders.Add(order);
        }

        await context.CommitAsync(CancellationToken.None);

        for (var i = 0; i < count; i++)
        {
            context.Add(OrderEmployeePay.Create(
                orders[i].Id, employees[i].Id, payPeriod.Id, basePay: 600m, totalPay: 600m));
        }

        await context.CommitAsync(CancellationToken.None);

        EmployeeIds.AddRange(employees.Select(e => e.Id));
        _payPeriodId = payPeriod.Id;
    }

    private static Order NewOrder(string ownerUserId, string customerEmail)
    {
        var address = Address.Create("Order St 9", "Brno", "60200", CountryId);
        var order = Order.Create(
            customerName: "Order Owner",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: ownerUserId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
