using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// ONE INVOICE PER (EMPLOYEE, PERIOD, CURRENCY), over real Postgres through the real command. A cleaner
/// who worked a CZK job and a EUR job in one period holds pay in two units; a tax document is in one.
/// The period used to be refused outright -- a refusal no admin action could resolve, because a EUR
/// job cannot be made a CZK job -- and the rows sat un-invoiced forever while the reconciliation sweep
/// re-enqueued the pair every tick. Now the period yields two documents, each with its own payout
/// reference, and the pair drops out of the sweep.
/// </summary>
[Collection("PostgresCollection")]
public class MixedCurrencyPeriodInvoicingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-mixed";
    private const string Czk = "currency-czk-mixed";
    private const string Eur = "currency-eur-mixed";

    private static string _employeeId = string.Empty;
    private static string _payPeriodId = string.Empty;

    [Fact]
    public async Task A_Period_With_Pay_In_Two_Currencies_Yields_One_Invoice_Per_Currency()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GenerateInvoice.Command(_employeeId, _payPeriodId)),
            assert: async (CleansiaDbContext context, BusinessResult<GenerateInvoice.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"GenerateInvoice failed with: {result.Error?.Message}");
                Assert.Equal(2, result.Value!.InvoiceIds.Count);

                var invoices = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Where(i => i.EmployeeId == _employeeId && i.PayPeriodId == _payPeriodId)
                    .ToListAsync();

                Assert.Equal(2, invoices.Count);
                var czk = invoices.Single(i => i.CurrencyId == Czk);
                var eur = invoices.Single(i => i.CurrencyId == Eur);
                Assert.Equal(1000m, czk.TotalAmount);
                Assert.Equal(30m, eur.TotalAmount);

                // Two documents, two references -- the allocator is a global counter, so they differ.
                Assert.NotNull(czk.VariableSymbol);
                Assert.NotNull(eur.VariableSymbol);
                Assert.NotEqual(czk.VariableSymbol, eur.VariableSymbol);

                // Every row is assigned to the invoice in ITS currency; none is left behind.
                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.EmployeeId == _employeeId && p.PayPeriodId == _payPeriodId)
                    .ToListAsync();
                Assert.All(pays.Where(p => p.CurrencyId == Czk), p => Assert.Equal(czk.Id, p.EmployeeInvoiceId));
                Assert.All(pays.Where(p => p.CurrencyId == Eur), p => Assert.Equal(eur.Id, p.EmployeeInvoiceId));

                // And the pair is no longer a reconciliation candidate: nothing is un-invoiced in any
                // currency, so the sweep has nothing to re-enqueue.
                var candidates = await new PayPeriodRepository(context)
                    .GetInvoiceReconciliationCandidatesAsync(DateTime.UtcNow.AddDays(1), take: 100, CancellationToken.None);
                Assert.DoesNotContain(candidates, c => c.EmployeeId == _employeeId && c.PayPeriodId == _payPeriodId);
            },
            transactional: false);
    }

    /// <summary>
    /// A second delivery of the same message is a no-op, not a duplicate: nothing is unassigned after a
    /// full run, so the "nothing to invoice" rule refuses it before any reference is claimed.
    /// </summary>
    [Fact]
    public async Task Invoicing_The_Same_Period_Again_Writes_Nothing_More()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var first = await mediator.Send(new GenerateInvoice.Command(_employeeId, _payPeriodId));
                Assert.True(first.IsSuccess, $"first GenerateInvoice failed with: {first.Error?.Message}");
                return await mediator.Send(new GenerateInvoice.Command(_employeeId, _payPeriodId));
            },
            assert: async (CleansiaDbContext context, BusinessResult<GenerateInvoice.Response> second) =>
            {
                Assert.False(second.IsSuccess);
                Assert.Equal(2, await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .CountAsync(i => i.EmployeeId == _employeeId && i.PayPeriodId == _payPeriodId));
            },
            transactional: false);
    }

    /// <summary>
    /// A pair whose CZK pay is already invoiced and whose EUR pay is not is STILL a candidate for the
    /// sweep -- the anti-join is per currency -- and invoicing it produces exactly the missing document.
    /// </summary>
    [Fact]
    public async Task A_Currency_Invoiced_Later_Is_Swept_And_Invoiced_On_Its_Own()
    {
        await TestMethod(
            arrange: async context =>
            {
                await SeedAsync(context);
                // Invoice the CZK half by hand, as a period closed before the EUR job landed would have.
                var czkPays = await context.Set<OrderEmployeePay>()
                    .Where(p => p.EmployeeId == _employeeId && p.PayPeriodId == _payPeriodId && p.CurrencyId == Czk)
                    .ToListAsync();
                var czkInvoice = EmployeeInvoice.CreateFromOrderPays(_employeeId, _payPeriodId, czkPays, "2600000001");
                context.Add(czkInvoice);
                foreach (var pay in czkPays)
                {
                    pay.AssignToInvoice(czkInvoice.Id);
                }
                await context.CommitAsync(CancellationToken.None);

                var candidates = await new PayPeriodRepository(context)
                    .GetInvoiceReconciliationCandidatesAsync(DateTime.UtcNow.AddDays(1), take: 100, CancellationToken.None);
                Assert.Contains(candidates, c => c.EmployeeId == _employeeId && c.PayPeriodId == _payPeriodId);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GenerateInvoice.Command(_employeeId, _payPeriodId)),
            assert: async (CleansiaDbContext context, BusinessResult<GenerateInvoice.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"GenerateInvoice failed with: {result.Error?.Message}");
                var written = Assert.Single(result.Value!.InvoiceIds);
                var invoice = await context.Set<EmployeeInvoice>().IgnoreQueryFilters().SingleAsync(i => i.Id == written);
                Assert.Equal(Eur, invoice.CurrencyId);
                Assert.Equal(30m, invoice.TotalAmount);
            },
            transactional: false);
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = Eur;
        eur.IsActive = true;

        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.AddRange(czk, eur);

        var payPeriod = PayPeriod.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15));
        context.Add(payPeriod);

        var user = User.CreateWithPassword(
            "mixed-emp@cleansia.test", "12345678Test!", "Emp", "Mixed", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var czkOrderA = NewOrder(user.Id, "mixed-buyer-a@cleansia.test", Czk);
        var czkOrderB = NewOrder(user.Id, "mixed-buyer-b@cleansia.test", Czk);
        var eurOrder = NewOrder(user.Id, "mixed-buyer-c@cleansia.test", Eur);
        context.AddRange(czkOrderA, czkOrderB, eurOrder);

        await context.CommitAsync(CancellationToken.None);

        context.AddRange(
            OrderEmployeePay.Create(czkOrderA.Id, employee.Id, payPeriod.Id, Czk, basePay: 600m, totalPay: 600m),
            OrderEmployeePay.Create(czkOrderB.Id, employee.Id, payPeriod.Id, Czk, basePay: 400m, totalPay: 400m),
            OrderEmployeePay.Create(eurOrder.Id, employee.Id, payPeriod.Id, Eur, basePay: 30m, totalPay: 30m));
        await context.CommitAsync(CancellationToken.None);

        _employeeId = employee.Id;
        _payPeriodId = payPeriod.Id;
    }

    private static Order NewOrder(string ownerUserId, string customerEmail, string currencyId)
    {
        var address = Address.Create("Order St 9", "Brno", "60200", CountryId);
        var order = Order.Create(
            customerName: "Order Owner",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: ownerUserId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
