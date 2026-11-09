using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The partial unique indexes of the pay tables, against the migration-built Postgres schema: a company holds one
/// open pay period, a cancelled invoice frees its cleaner, period and currency for the corrected one, and a dispute
/// charge carried to a later invoice sits beside the job's pay row with one of each per order and cleaner.
/// </summary>
[Collection("PostgresCollection")]
public class PayrollUniqueIndexTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-pay-index";
    private const string CurrencyId = "currency-czk-pay-index";
    private const PayLineType DisputeCharge = (PayLineType)3;

    private sealed record Graph(string EmployeeId, string OrderId, string PeriodId);

    /// <summary>
    /// The night run and Close now both close the open period and add its successor in one save. A second open
    /// period added on its own is refused, and another company's is not.
    /// </summary>
    [Fact]
    public async Task A_Company_Holds_One_Open_Period_And_Closing_It_Opens_The_Next_In_One_Save()
    {
        var open = PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14));

        await TestMethod(
            arrange: async context =>
            {
                context.Add(open);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var second = PayPeriod.Create(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 28));
                context.Add(second);
                var refused = await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
                context.Remove(second);

                var period = await context.PayPeriods.SingleAsync(p => p.Id == open.Id);
                period.Close("System");
                context.Add(PayPeriod.CreateBiWeekly(period.EndDate.AddDays(1)));
                await context.CommitAsync(CancellationToken.None);

                var otherCompany = PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14));
                otherCompany.TenantId = TestTenants.Second;
                context.Add(otherCompany);
                await context.CommitAsync(CancellationToken.None);

                return DbConstraintViolation.IsUniqueViolation(refused);
            },
            assert: async (CleansiaDbContext context, bool refusedByTheIndex) =>
            {
                Assert.True(refusedByTheIndex);

                var periods = await context.PayPeriods.IgnoreQueryFilters().ToListAsync();
                Assert.Equal(PayPeriodStatus.Closed, periods.Single(p => p.Id == open.Id).Status);
                Assert.Equal(
                    [TestTenants.Default, TestTenants.Second],
                    periods.Where(p => p.Status == PayPeriodStatus.Open).Select(p => p.TenantId).Order());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Cancelled_Invoice_Frees_Its_Slot_For_The_Corrected_One()
    {
        Graph graph = default!;

        await TestMethod(
            arrange: async context =>
            {
                graph = await SeedAsync(context);
                context.Add(Invoice(graph).Cancel("Wrong amount", "admin@cleansia.test"));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();

                context.Add(Invoice(graph));
                await context.CommitAsync(CancellationToken.None);

                var duplicate = Invoice(graph);
                context.Add(duplicate);
                var refused = await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
                context.Remove(duplicate);

                return DbConstraintViolation.IsUniqueViolation(refused);
            },
            assert: async (CleansiaDbContext context, bool refusedByTheIndex) =>
            {
                Assert.True(refusedByTheIndex);

                var statuses = await context.EmployeeInvoices.IgnoreQueryFilters().Select(i => i.Status).ToListAsync();
                Assert.Equal([EmployeeInvoiceStatus.Pending, EmployeeInvoiceStatus.Cancelled], statuses.Order());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Dispute_Charge_Line_Sits_Beside_The_Job_Row_And_Each_Stays_One_Of_A_Kind()
    {
        Graph graph = default!;

        await TestMethod(
            arrange: async context => graph = await SeedAsync(context),
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();

                context.AddRange(Pay(graph, PayLineType.Job), Pay(graph, DisputeCharge));
                await context.CommitAsync(CancellationToken.None);

                var refusals = new List<bool>();
                foreach (var lineType in new[] { PayLineType.Job, DisputeCharge })
                {
                    var second = Pay(graph, lineType);
                    context.Add(second);
                    var refused = await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
                    context.Remove(second);
                    refusals.Add(DbConstraintViolation.IsUniqueViolation(refused));
                }

                return refusals;
            },
            assert: async (CleansiaDbContext context, List<bool> refusals) =>
            {
                Assert.Equal([true, true], refusals);
                Assert.Equal(2, await context.OrderEmployeePays.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    private static EmployeeInvoice Invoice(Graph graph) =>
        EmployeeInvoice.Create(
            graph.EmployeeId,
            graph.PeriodId,
            totalOrders: 1,
            subTotal: 600m,
            currencyId: CurrencyId,
            variableSymbol: PayrollMockFactory.NextTestVariableSymbol(),
            invoiceNumber: PayrollMockFactory.NextTestInvoiceNumber());

    private static OrderEmployeePay Pay(Graph graph, PayLineType lineType) =>
        OrderEmployeePay.Create(
            graph.OrderId, graph.EmployeeId, graph.PeriodId, CurrencyId, basePay: 600m, totalPay: 600m, lineType: lineType);

    private static async Task<Graph> SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;

        var user = User.CreateWithPassword("pay-index@cleansia.test", "12345678Test!", "Pay", "Index", UserProfile.Employee);
        var employee = Employee.CreateWithUser(user);
        var period = PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14)).Close("System");
        var order = Order.Create(
            customerName: "Pay Index",
            customerEmail: "pay-index-customer@cleansia.test",
            customerPhone: "+420777333445",
            customerAddress: Address.Create("Index St 1", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);

        context.AddRange(Language.Create("en", "English"), country, currency, user, employee, period, order);
        await context.CommitAsync(CancellationToken.None);

        return new Graph(employee.Id, order.Id, period.Id);
    }
}
