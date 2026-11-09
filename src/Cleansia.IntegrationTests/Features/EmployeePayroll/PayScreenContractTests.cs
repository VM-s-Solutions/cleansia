using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// What the pay screens read from the database: the dashboard's current figure is the open period's pay that no
/// invoice holds, in one currency, and the invoice detail carries its period's dates and the constant symbol the
/// invoice PDF prints, from the cleaner's country.
/// </summary>
[Collection("PostgresCollection")]
public class PayScreenContractTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-payscreen";
    private const string CzkId = "currency-czk-payscreen";
    private const string EurId = "currency-eur-payscreen";

    private static string _employeeId = string.Empty;
    private static string _openPeriodId = string.Empty;
    private static string _invoiceId = string.Empty;

    /// <summary>
    /// 600 + 400 of CZK sit un-invoiced in the open period. The 250 a cancel freed in the closed period, the 30 in
    /// EUR and the invoiced 700 are not this period's figure.
    /// </summary>
    [Fact]
    public async Task The_Current_Figure_Sums_Only_The_Open_Periods_Uninvoiced_Pay_In_Its_Currency()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: provider => provider.GetRequiredService<IOrderEmployeePayRepository>()
                .SumPendingEarningsAsync(_employeeId, _openPeriodId, CzkId, CancellationToken.None),
            assert: (CleansiaDbContext _, decimal sum) =>
            {
                Assert.Equal(1000m, sum);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Invoice_Detail_Carries_Its_Periods_Dates_And_The_Countrys_Constant_Symbol()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: provider => provider.GetRequiredService<IMediator>().Send(new GetInvoiceById.Query(_invoiceId)),
            assert: (CleansiaDbContext _, BusinessResult<EmployeeInvoiceDetailDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var detail = result.Value!;
                Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 14)), (detail.PayPeriodStartDate, detail.PayPeriodEndDate));
                Assert.Equal("0308", detail.ConstantSymbol);
                return Task.CompletedTask;
            });
    }

    private static Task ReplaceWithAdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-payscreen",
            "admin-payscreen@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = EurId;
        eur.IsActive = true;
        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.AddRange(czk, eur);
        context.Add(CountryInvoiceConfig.Create(CountryId, vatRequired: true, vatRate: 0.21m, constantSymbol: "0308"));

        var closed = PayPeriod.CreateBiWeekly(new DateOnly(2026, 3, 1)).Close("System");
        var open = PayPeriod.CreateBiWeekly(new DateOnly(2026, 3, 15));
        context.AddRange(closed, open);

        var user = User.CreateWithPassword(
            "payscreen-emp@cleansia.test", "12345678Test!", "Pay", "Screen", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        employee.UpdateAddress(Address.Create("Home St 1", "Brno", "60200", CountryId));
        context.Add(employee);

        var orders = Enumerable.Range(1, 5)
            .Select(n => NewOrder(user.Id, $"payscreen-{n}@cleansia.test", n == 4 ? EurId : CzkId))
            .ToArray();
        context.AddRange(orders);
        await context.CommitAsync(CancellationToken.None);

        var invoiced = OrderEmployeePay.Create(orders[4].Id, employee.Id, closed.Id, CzkId, basePay: 700m, totalPay: 700m);
        context.AddRange(
            OrderEmployeePay.Create(orders[0].Id, employee.Id, open.Id, CzkId, basePay: 600m, totalPay: 600m),
            OrderEmployeePay.Create(orders[1].Id, employee.Id, open.Id, CzkId, basePay: 400m, totalPay: 400m),
            OrderEmployeePay.Create(orders[2].Id, employee.Id, closed.Id, CzkId, basePay: 250m, totalPay: 250m),
            OrderEmployeePay.Create(orders[3].Id, employee.Id, open.Id, EurId, basePay: 30m, totalPay: 30m),
            invoiced);
        await context.CommitAsync(CancellationToken.None);

        var invoice = EmployeeInvoice.CreateFromOrderPays(
            employee.Id, closed.Id, [invoiced], PayrollMockFactory.NextTestVariableSymbol(), PayrollMockFactory.NextTestInvoiceNumber());
        context.Add(invoice);
        invoiced.AssignToInvoice(invoice.Id);
        await context.CommitAsync(CancellationToken.None);

        _employeeId = employee.Id;
        _openPeriodId = open.Id;
        _invoiceId = invoice.Id;
    }

    private static Order NewOrder(string ownerUserId, string customerEmail, string currencyId)
    {
        var order = Order.Create(
            customerName: "Order Owner",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Order St 9", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: ownerUserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
