using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// Two administrators act on one approved invoice: both read it approved, one marks it paid and commits, then
/// the other's cancel commits. The cancel is refused with a business error, so the paid invoice keeps its jobs
/// and its cash set-off, and the cash is not handed back to be set off a second time by a reissue.
/// </summary>
[Collection("PostgresCollection")]
public class InvoiceConcurrentAdminActionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-invoice-race";
    private const string CurrencyId = "currency-czk-invoice-race";

    private static string _employeeId = string.Empty;
    private static string _payPeriodId = string.Empty;

    [Fact]
    public async Task A_Cancel_That_Read_The_Invoice_Before_A_Mark_Paid_Committed_Is_Refused_And_Sets_Nothing_Back()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var generated = await SendAsync(provider, new GenerateInvoice.Command(_employeeId, _payPeriodId));
                Assert.True(generated.IsSuccess, $"GenerateInvoice failed with: {generated.Error?.Message}");
                var invoiceId = Assert.Single(generated.Value!.InvoiceIds);

                using (var scope = provider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var invoice = await context.Set<EmployeeInvoice>().SingleAsync(i => i.Id == invoiceId);
                    invoice.Approve("admin@cleansia.test");
                    await context.CommitAsync(CancellationToken.None);
                }

                using var cancellingAdmin = provider.CreateScope();
                await cancellingAdmin.ServiceProvider.GetRequiredService<IEmployeeInvoiceRepository>()
                    .GetByIdAsync(invoiceId, CancellationToken.None);

                var paid = await SendAsync(provider, new MarkInvoicePaid.Command(invoiceId, "Transfer 1", null));
                Assert.True(paid.IsSuccess, $"MarkInvoicePaid failed with: {paid.Error?.Message}");

                var cancelled = await cancellingAdmin.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CancelInvoice.Command(invoiceId, "Wrong deduction"));
                return (InvoiceId: invoiceId, Cancelled: cancelled);
            },
            assert: async (CleansiaDbContext context, (string InvoiceId, BusinessResult<CancelInvoice.Response> Cancelled) result) =>
            {
                Assert.True(result.Cancelled.IsFailure);
                var refusal = Assert.IsAssignableFrom<IValidationResult>(result.Cancelled);
                Assert.Contains(refusal.Errors, e => e.Message == BusinessErrorMessage.InvoiceChangedConcurrently);

                var invoice = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .SingleAsync(i => i.Id == result.InvoiceId);
                Assert.Equal(EmployeeInvoiceStatus.Paid, invoice.Status);
                Assert.False(invoice.IsCancelled);
                Assert.Equal((1000m, 250m, 750m), (invoice.TotalAmount, invoice.CashSetOffAmount, invoice.TransferAmount));

                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.EmployeeId == _employeeId)
                    .ToListAsync();
                Assert.All(pays, p => Assert.Equal(result.InvoiceId, p.EmployeeInvoiceId));

                // 250 collected and set off once by the paid invoice; nothing given back.
                var ledger = await context.Set<CashLedgerEntry>()
                    .IgnoreQueryFilters()
                    .Where(e => e.EmployeeId == _employeeId && e.CurrencyId == CurrencyId)
                    .Select(e => e.Amount)
                    .ToListAsync();
                Assert.Equal([-250m, 250m], ledger.Order());
            },
            transactional: false);
    }

    private static async Task<BusinessResult<TResponse>> SendAsync<TResponse>(
        IServiceProvider provider, IRequest<BusinessResult<TResponse>> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var payPeriod = PayPeriod.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 14)).Close("System");
        context.Add(payPeriod);

        var user = User.CreateWithPassword("invoice-race-emp@cleansia.test", "12345678Test!", "Emp", "Race", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var cashJob = NewOrder("invoice-race-buyer-a@cleansia.test");
        cashJob.MarkCashCollected(employee.Id, amount: 250m);
        var cardJob = NewOrder("invoice-race-buyer-b@cleansia.test");
        context.AddRange(cashJob, cardJob);
        await context.CommitAsync(CancellationToken.None);

        context.AddRange(
            CashLedgerEntry.ForCollection(cashJob),
            OrderEmployeePay.Create(cashJob.Id, employee.Id, payPeriod.Id, CurrencyId, basePay: 600m, totalPay: 600m),
            OrderEmployeePay.Create(cardJob.Id, employee.Id, payPeriod.Id, CurrencyId, basePay: 400m, totalPay: 400m));
        await context.CommitAsync(CancellationToken.None);

        _employeeId = employee.Id;
        _payPeriodId = payPeriod.Id;
    }

    private static Order NewOrder(string customerEmail)
    {
        var order = Order.Create(
            customerName: "Invoice Race Customer",
            customerEmail: customerEmail,
            customerPhone: "+420777333445",
            customerAddress: Address.Create("Order St 10", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        return order;
    }
}
