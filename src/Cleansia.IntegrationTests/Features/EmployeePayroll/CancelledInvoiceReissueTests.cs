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
/// Cancelling an invoice frees its jobs, and the safety net issues the corrected invoice under a new number with
/// the same total and the same cash set-off. Each step runs in a scope of its own, as separate requests do,
/// over the migration-built Postgres and the sweep's own candidate query.
/// </summary>
[Collection("PostgresCollection")]
public class CancelledInvoiceReissueTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-reissue";
    private const string CurrencyId = "currency-czk-reissue";

    private static string _employeeId = string.Empty;
    private static string _payPeriodId = string.Empty;

    [Fact]
    public async Task A_Cancelled_Invoice_Frees_Its_Jobs_And_The_Corrected_Invoice_Takes_The_Same_Total_And_Set_Off()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var first = await SendAsync(provider, new GenerateInvoice.Command(_employeeId, _payPeriodId));
                Assert.True(first.IsSuccess, $"GenerateInvoice failed with: {first.Error?.Message}");
                var firstId = Assert.Single(first.Value!.InvoiceIds);

                var cancelled = await SendAsync(provider, new CancelInvoice.Command(firstId, "Wrong deduction"));
                Assert.True(cancelled.IsSuccess, $"CancelInvoice failed with: {cancelled.Error?.Message}");

                using (var scope = provider.CreateScope())
                {
                    var candidates = await scope.ServiceProvider.GetRequiredService<IPayPeriodRepository>()
                        .GetInvoiceReconciliationCandidatesAsync(DateTime.UtcNow.AddDays(1), take: 50, CancellationToken.None);
                    Assert.Contains(candidates, c => c.EmployeeId == _employeeId && c.PayPeriodId == _payPeriodId);
                }

                var second = await SendAsync(provider, new GenerateInvoice.Command(_employeeId, _payPeriodId));
                Assert.True(second.IsSuccess, $"the corrected GenerateInvoice failed with: {second.Error?.Message}");
                return (FirstId: firstId, SecondId: Assert.Single(second.Value!.InvoiceIds));
            },
            assert: async (CleansiaDbContext context, (string FirstId, string SecondId) ids) =>
            {
                var invoices = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Where(i => i.EmployeeId == _employeeId)
                    .ToListAsync();
                var first = invoices.Single(i => i.Id == ids.FirstId);
                var second = invoices.Single(i => i.Id == ids.SecondId);

                Assert.Equal(EmployeeInvoiceStatus.Cancelled, first.Status);
                Assert.Equal(EmployeeInvoiceStatus.Pending, second.Status);
                Assert.NotEqual(first.InvoiceNumber, second.InvoiceNumber);
                Assert.Equal((1000m, 250m, 750m), (first.TotalAmount, first.CashSetOffAmount, first.TransferAmount));
                Assert.Equal((1000m, 250m, 750m), (second.TotalAmount, second.CashSetOffAmount, second.TransferAmount));

                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.EmployeeId == _employeeId)
                    .ToListAsync();
                Assert.Equal(2, pays.Count);
                Assert.All(pays, p => Assert.Equal(ids.SecondId, p.EmployeeInvoiceId));

                // 250 collected, set off by the first invoice, given back by its cancel, set off again by the second.
                var ledger = await context.Set<CashLedgerEntry>()
                    .IgnoreQueryFilters()
                    .Where(e => e.EmployeeId == _employeeId && e.CurrencyId == CurrencyId)
                    .Select(e => e.Amount)
                    .ToListAsync();
                Assert.Equal([-250m, -250m, 250m, 250m], ledger.Order());
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

        var user = User.CreateWithPassword("reissue-emp@cleansia.test", "12345678Test!", "Emp", "Reissue", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var cashJob = NewOrder("reissue-buyer-a@cleansia.test");
        cashJob.MarkCashCollected(employee.Id, amount: 250m);
        var cardJob = NewOrder("reissue-buyer-b@cleansia.test");
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
            customerName: "Reissue Customer",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Order St 9", "Brno", "60200", CountryId),
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
