using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Features.EmployeePayroll.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using SortDefinition = Cleansia.Core.AppServices.Shared.DTOs.Sorting.SortDefinition;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The admin invoice list across two currencies — the invoice twin of <c>OrderListCurrencyScopeTests</c>:
/// a currency filter isolates one, and an amount sort with no currency filter is "amount within
/// currency". And the pay rows behind My Pay carry their currency as a real navigation on the existing
/// FK, so the per-row code the DTO now names is populated by the repository rather than left null.
/// </summary>
[Collection("PostgresCollection")]
public class InvoiceListCurrencyScopeTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-ilscope";
    private const string CzkId = "currency-czk-ilscope";
    private const string EurId = "currency-eur-ilscope";

    private static string _employeeId = string.Empty;
    private static string _payPeriodId = string.Empty;

    [Fact]
    public async Task A_Currency_Filter_Isolates_That_Currency()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedInvoices.Request
                {
                    Filter = new EmployeeInvoiceFilter(EmployeeId: null, PayPeriodId: null, Statuses: null, CurrencyId: EurId),
                }),
            assert: (CleansiaDbContext _, PagedData<EmployeeInvoiceDto> page) =>
            {
                Assert.Equal(1, page.Total);
                Assert.Equal("EUR", Assert.Single(page.Data!).CurrencyCode);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task An_Amount_Sort_With_No_Currency_Filter_Is_Amount_Within_Currency()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedInvoices.Request
                {
                    Filter = new EmployeeInvoiceFilter(EmployeeId: null, PayPeriodId: null, Statuses: null),
                    Sort = [new SortDefinition("totalAmount", SortDirection.Descending)],
                }),
            assert: (CleansiaDbContext _, PagedData<EmployeeInvoiceDto> page) =>
            {
                var rows = page.Data!.ToList();
                Assert.Equal(3, rows.Count);

                var codes = rows.Select(r => r.CurrencyCode).ToList();
                var boundary = codes.FindIndex(code => code != codes[0]);
                Assert.True(boundary > 0);
                Assert.All(codes.Skip(boundary), code => Assert.NotEqual(codes[0], code));

                foreach (var group in rows.GroupBy(r => r.CurrencyCode))
                {
                    var amounts = group.Select(r => r.TotalAmount).ToList();
                    Assert.Equal(amounts.OrderByDescending(a => a), amounts);
                }

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The navigation maps onto the existing <c>FK_OrderEmployeePays_Currencies_CurrencyId</c> — no
    /// second shadow key — and the repository includes it, so every row My Pay renders names its unit.
    /// </summary>
    [Fact]
    public async Task Pay_Rows_Carry_Their_Currency_And_My_Pay_Names_It_On_Every_Row()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider =>
            {
                var rows = await provider.GetRequiredService<IOrderEmployeePayRepository>()
                    .GetByEmployeeAndPeriodAsync(_employeeId, _payPeriodId, CancellationToken.None);
                // Snapshotted BEFORE anything else in this scope tracks a Currency: a later query that
                // loads one would fix the navigation up on these same instances and hide a dropped
                // Include.
                var loaded = rows.Select(r => (r.CurrencyId, Code: r.Currency?.Code)).ToList();
                var myPay = await provider.GetRequiredService<IMediator>()
                    .Send(new GetPeriodPays.Query(_employeeId, _payPeriodId, CurrencyId: EurId));
                return (Loaded: loaded, MyPay: myPay);
            },
            assert: (CleansiaDbContext _, (List<(string CurrencyId, string? Code)> Loaded, BusinessResult<PeriodPaySummaryDto> MyPay) result) =>
            {
                Assert.Equal(3, result.Loaded.Count);
                Assert.All(result.Loaded, row => Assert.Equal(row.CurrencyId == EurId ? "EUR" : "CZK", row.Code));

                Assert.True(result.MyPay.IsSuccess);
                var summary = result.MyPay.Value!;
                Assert.Equal("EUR", summary.CurrencyCode);
                Assert.Equal(30m, summary.GrandTotal);
                Assert.All(summary.OrderPays, row => Assert.Equal("EUR", row.CurrencyCode));

                return Task.CompletedTask;
            });
    }

    private static Task ReplaceWithAdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-ilscope",
            "admin-ilscope@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", isServiced: true);
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

        var payPeriod = PayPeriod.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15));
        var otherPeriod = PayPeriod.Create(new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 31));
        context.AddRange(payPeriod, otherPeriod);

        var user = User.CreateWithPassword(
            "ilscope-emp@cleansia.test", "12345678Test!", "Emp", "Scope", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var czkOrderA = NewOrder(user.Id, "ilscope-a@cleansia.test", CzkId);
        var czkOrderB = NewOrder(user.Id, "ilscope-b@cleansia.test", CzkId);
        var eurOrder = NewOrder(user.Id, "ilscope-c@cleansia.test", EurId);
        var czkOrderLater = NewOrder(user.Id, "ilscope-d@cleansia.test", CzkId);
        context.AddRange(czkOrderA, czkOrderB, eurOrder, czkOrderLater);
        await context.CommitAsync(CancellationToken.None);

        var czkPays = new[]
        {
            OrderEmployeePay.Create(czkOrderA.Id, employee.Id, payPeriod.Id, CzkId, basePay: 600m, totalPay: 600m),
            OrderEmployeePay.Create(czkOrderB.Id, employee.Id, payPeriod.Id, CzkId, basePay: 400m, totalPay: 400m),
        };
        var eurPays = new[]
        {
            OrderEmployeePay.Create(eurOrder.Id, employee.Id, payPeriod.Id, EurId, basePay: 30m, totalPay: 30m),
        };
        var laterPays = new[]
        {
            OrderEmployeePay.Create(czkOrderLater.Id, employee.Id, otherPeriod.Id, CzkId, basePay: 20m, totalPay: 20m),
        };
        context.AddRange(czkPays);
        context.AddRange(eurPays);
        context.AddRange(laterPays);
        await context.CommitAsync(CancellationToken.None);

        // One invoice per (employee, period, currency): 1000 CZK, 30 EUR, and 20 CZK in the next period --
        // amounts chosen so a plain cross-currency sort would interleave them (1000 CZK, 30 EUR, 20 CZK).
        AddInvoice(context, employee.Id, payPeriod.Id, czkPays, "2600000101");
        AddInvoice(context, employee.Id, payPeriod.Id, eurPays, "2600000102");
        AddInvoice(context, employee.Id, otherPeriod.Id, laterPays, "2600000103");
        await context.CommitAsync(CancellationToken.None);

        _employeeId = employee.Id;
        _payPeriodId = payPeriod.Id;
    }

    private static void AddInvoice(
        CleansiaDbContext context, string employeeId, string payPeriodId, OrderEmployeePay[] pays, string variableSymbol)
    {
        var invoice = EmployeeInvoice.CreateFromOrderPays(employeeId, payPeriodId, pays, variableSymbol);
        context.Add(invoice);
        foreach (var pay in pays)
        {
            pay.AssignToInvoice(invoice.Id);
        }
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
            userId: ownerUserId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
