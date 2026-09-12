using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Reports;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Reports;

/// <summary>
/// An admin report is ONE currency. Every figure on it is a sum, and a sum across two currencies is
/// not a number, so the admin chooses one (or takes the platform default), the query filters on it in
/// SQL, and the answer names it. Proven over real Postgres: two CZK orders and one EUR order in the
/// window, and the report answers 45.10 EUR or 2100 CZK -- never 2145.10 of nothing (T-0702).
/// </summary>
[Collection("PostgresCollection")]
public class AdminReportCurrencyScopeTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-report";
    private const string Czk = "currency-czk-report";
    private const string Eur = "currency-eur-report";

    private static readonly DateTime Start = DateTime.UtcNow.AddDays(-1);
    private static readonly DateTime End = DateTime.UtcNow.AddDays(5);

    [Fact]
    public async Task Revenue_Report_Answers_In_One_Currency_And_Names_It()
    {
        await TestMethod(
            arrange: SeedOrdersAsync,
            act: provider => Revenue(provider, Eur),
            assert: (CleansiaDbContext _, BusinessResult<RevenueReportDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var report = result.Value!;
                Assert.Equal(45.10m, report.TotalRevenue);
                Assert.Equal(1, report.TotalOrders);
                Assert.Equal(45.10m, report.AverageOrderValue);
                Assert.Equal("EUR", report.CurrencyCode);
                Assert.Equal(45.10m, report.RevenueByPaymentType.Sum(r => r.TotalRevenue));
                Assert.Equal(45.10m, report.DailyRevenues.Sum(d => d.Amount));
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task Revenue_Report_Defaults_To_The_Platform_Default_Currency()
    {
        await TestMethod(
            arrange: SeedOrdersAsync,
            act: provider => Revenue(provider, null),
            assert: (CleansiaDbContext _, BusinessResult<RevenueReportDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(2100m, result.Value!.TotalRevenue);
                Assert.Equal(2, result.Value.TotalOrders);
                Assert.Equal("CZK", result.Value.CurrencyCode);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task Revenue_Report_Refuses_A_Currency_That_Does_Not_Exist()
    {
        await TestMethod(
            arrange: SeedOrdersAsync,
            act: provider => Revenue(provider, "no-such-currency"),
            assert: (CleansiaDbContext _, BusinessResult<RevenueReportDto> result) =>
            {
                Assert.True(result.IsFailure);
                // The validator refuses it; the pipeline wraps that as a validation result.
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.CurrencyNotFound);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task Payroll_Report_Answers_In_One_Currency_And_Names_It()
    {
        await TestMethod(
            arrange: SeedInvoicesAsync,
            act: provider => Payroll(provider, Eur),
            assert: (CleansiaDbContext _, BusinessResult<PayrollReportDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var report = result.Value!;
                Assert.Equal(20m, report.TotalPayroll);
                Assert.Equal(1, report.TotalInvoices);
                Assert.Equal(20m, report.EmployeeSummaries.Single().TotalAmount);
                Assert.Equal(20m, report.MonthlyPayroll.Sum(m => m.TotalAmount));
                Assert.Equal("EUR", report.CurrencyCode);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task Payroll_Report_Defaults_To_The_Platform_Default_Currency()
    {
        await TestMethod(
            arrange: SeedInvoicesAsync,
            act: provider => Payroll(provider, null),
            assert: (CleansiaDbContext _, BusinessResult<PayrollReportDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(500m, result.Value!.TotalPayroll);
                Assert.Equal(1, result.Value.TotalInvoices);
                Assert.Equal("CZK", result.Value.CurrencyCode);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private static Task<BusinessResult<RevenueReportDto>> Revenue(IServiceProvider provider, string? currencyId) =>
        provider.GetRequiredService<IMediator>()
            .Send(new GetRevenueReport.Query(new ReportFilter(Start, End, currencyId)));

    private static Task<BusinessResult<PayrollReportDto>> Payroll(IServiceProvider provider, string? currencyId) =>
        provider.GetRequiredService<IMediator>()
            .Send(new GetPayrollReport.Query(new ReportFilter(Start, End, currencyId)));

    private static void SeedCurrencies(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = Eur;
        eur.IsActive = true;
        context.Currencies.AddRange(czk, eur);
    }

    private static async Task SeedOrdersAsync(CleansiaDbContext context)
    {
        SeedCurrencies(context);
        context.AddRange(
            NewOrder("report-czk-a@cleansia.test", Czk, 1000m),
            NewOrder("report-czk-b@cleansia.test", Czk, 1100m),
            NewOrder("report-eur-a@cleansia.test", Eur, 45.10m));
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedInvoicesAsync(CleansiaDbContext context)
    {
        SeedCurrencies(context);

        var user = User.CreateWithPassword("report-emp@cleansia.test", "12345678Test!", "Rep", "Ort", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var period = PayPeriod.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15));
        context.Add(period);
        await context.CommitAsync(CancellationToken.None);

        context.AddRange(
            EmployeeInvoice.Create(employee.Id, period.Id, 1, 500m, Czk, PayrollMockFactory.NextTestVariableSymbol()),
            EmployeeInvoice.Create(employee.Id, period.Id, 1, 20m, Eur, PayrollMockFactory.NextTestVariableSymbol()));
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string customerEmail, string currencyId, decimal totalPrice)
    {
        var order = Order.Create(
            customerName: "Report Customer",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Report St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Cash,
            totalPrice: totalPrice,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Pending);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
