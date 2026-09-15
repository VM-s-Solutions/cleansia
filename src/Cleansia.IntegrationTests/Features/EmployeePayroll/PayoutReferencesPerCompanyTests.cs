using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The 2026-09-15 ruling on the invoice table itself, against the migration-built Postgres schema: a
/// payout invoice's two references are unique PER OPERATING COMPANY. Two companies' first invoices
/// of a year carry the same strings and both commit; a second invoice with the same reference inside
/// ONE company is refused by the index. SQLite cannot model the filtered NULLS NOT DISTINCT index.
/// </summary>
[Collection("PostgresCollection")]
public class PayoutReferencesPerCompanyTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-per-company";
    private const string CurrencyId = "currency-czk-per-company";

    private static string _firstEmployeeId = default!;
    private static string _firstPeriodId = default!;
    private static string _firstSecondPeriodId = default!;
    private static string _secondEmployeeId = default!;
    private static string _secondPeriodId = default!;

    [Fact]
    public async Task The_Same_Variable_Symbol_Exists_Once_Per_Company_And_Not_Twice_Within_One()
    {
        await TestMethod(
            arrange: SeedTwoCompaniesAsync,
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();

                await AddAndCommitAsync(context, Invoice(_firstEmployeeId, _firstPeriodId, "2026000001", "INV-2026-000001"), TestTenants.Default);
                await AddAndCommitAsync(context, Invoice(_secondEmployeeId, _secondPeriodId, "2026000001", "INV-2026-000001"), TestTenants.Second);

                var duplicate = Invoice(_firstEmployeeId, _firstSecondPeriodId, "2026000001", "INV-2026-000002");
                var refused = await Assert.ThrowsAsync<DbUpdateException>(() =>
                    AddAndCommitAsync(context, duplicate, TestTenants.Default));
                context.Rollback();

                return DbConstraintViolation.IsUniqueViolation(refused);
            },
            assert: async (CleansiaDbContext context, bool refusedByTheIndex) =>
            {
                Assert.True(refusedByTheIndex);

                var symbols = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Select(i => new { i.TenantId, i.VariableSymbol })
                    .ToListAsync();

                Assert.Equal(2, symbols.Count);
                Assert.All(symbols, s => Assert.Equal("2026000001", s.VariableSymbol));
                Assert.Equal([TestTenants.Default, TestTenants.Second], symbols.Select(s => s.TenantId).Order());
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Same_Invoice_Number_Exists_Once_Per_Company_And_Not_Twice_Within_One()
    {
        await TestMethod(
            arrange: SeedTwoCompaniesAsync,
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();

                await AddAndCommitAsync(context, Invoice(_firstEmployeeId, _firstPeriodId, "2026000001", "INV-2026-000001"), TestTenants.Default);
                await AddAndCommitAsync(context, Invoice(_secondEmployeeId, _secondPeriodId, "2026000001", "INV-2026-000001"), TestTenants.Second);

                var duplicate = Invoice(_firstEmployeeId, _firstSecondPeriodId, "2026000002", "INV-2026-000001");
                var refused = await Assert.ThrowsAsync<DbUpdateException>(() =>
                    AddAndCommitAsync(context, duplicate, TestTenants.Default));
                context.Rollback();

                return DbConstraintViolation.IsUniqueViolation(refused);
            },
            assert: async (CleansiaDbContext context, bool refusedByTheIndex) =>
            {
                Assert.True(refusedByTheIndex);

                var numbers = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Select(i => new { i.TenantId, i.InvoiceNumber })
                    .ToListAsync();

                Assert.Equal(2, numbers.Count);
                Assert.All(numbers, n => Assert.Equal("INV-2026-000001", n.InvoiceNumber));
                Assert.Equal([TestTenants.Default, TestTenants.Second], numbers.Select(n => n.TenantId).Order());
            },
            transactional: false);
    }

    private static EmployeeInvoice Invoice(string employeeId, string payPeriodId, string variableSymbol, string invoiceNumber) =>
        EmployeeInvoice.Create(
            employeeId,
            payPeriodId,
            totalOrders: 0,
            subTotal: 0m,
            currencyId: CurrencyId,
            variableSymbol: variableSymbol,
            invoiceNumber: invoiceNumber);

    private static async Task AddAndCommitAsync(CleansiaDbContext context, EmployeeInvoice invoice, string tenantId)
    {
        invoice.TenantId = tenantId;
        context.Add(invoice);
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedTwoCompaniesAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);

        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var (firstEmployee, firstPeriod) = AddCompanyGraph(context, "per-company-cz");
        var firstSecondPeriod = PayPeriod.Create(new DateOnly(2026, 1, 16), new DateOnly(2026, 1, 31));
        context.Add(firstSecondPeriod);
        StampUnstampedAdded(context, TestTenants.Default);

        var (secondEmployee, secondPeriod) = AddCompanyGraph(context, "per-company-sk");
        StampUnstampedAdded(context, TestTenants.Second);

        await context.CommitAsync(CancellationToken.None);

        _firstEmployeeId = firstEmployee.Id;
        _firstPeriodId = firstPeriod.Id;
        _firstSecondPeriodId = firstSecondPeriod.Id;
        _secondEmployeeId = secondEmployee.Id;
        _secondPeriodId = secondPeriod.Id;
    }

    private static (Employee Employee, PayPeriod Period) AddCompanyGraph(CleansiaDbContext context, string slug)
    {
        var user = User.CreateWithPassword($"{slug}@cleansia.test", "12345678Test!", "Emp", "Loyee", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);

        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var period = PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        context.Add(period);

        return (employee, period);
    }
}
