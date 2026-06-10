using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The "generate-invoice queue is no longer dead" proof, driven end-to-end over a REAL Postgres
/// DbContext (Testcontainers). Unlike the handler unit test — which mocks <see cref="IMediator"/> and
/// asserts the consumer wiring — this runs the actual <c>GenerateInvoice.Command</c> through the real
/// MediatR pipeline (validator + handler + UnitOfWork commit) and real repositories.
///
/// <para>Asserts: a wellformed message persists exactly ONE <see cref="EmployeeInvoice"/> stamped with
/// the employee's TenantId, and the unpaid <see cref="OrderEmployeePay"/> rows are assigned to it. A
/// SECOND consume of the same message (at-least-once redelivery) leaves exactly one invoice — the
/// validator's already-exists guard makes the redelivery a no-op (TC-IDEMP-0 shape).</para>
/// </summary>
[Collection("PostgresCollection")]
public class GenerateInvoiceQueueConsumeTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string TenantId = "tenant-payroll-A";
    private const string CountryId = "country-cz-payroll";
    private const string CurrencyId = "currency-czk-payroll";

    private static string _employeeId = default!;
    private static string _payPeriodId = default!;

    [Fact]
    public async Task Consume_Persists_Exactly_One_Invoice_With_Tenant_And_Assigns_Pays_And_Is_Idempotent()
    {
        await TestMethod(
            arrange: SeedEmployeeWithUnpaidPays,
            act: async provider =>
            {
                var handler = NewConsumer(provider);
                var body = Enveloped(_employeeId, _payPeriodId, TenantId);

                // First consume — creates the invoice and assigns the pays.
                await handler.HandleAsync(body, CancellationToken.None);
                // Second consume of the SAME message — at-least-once redelivery must be a no-op.
                await handler.HandleAsync(body, CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var invoices = await context.Set<EmployeeInvoice>()
                    .IgnoreQueryFilters()
                    .Where(i => i.EmployeeId == _employeeId && i.PayPeriodId == _payPeriodId)
                    .ToListAsync();

                // TC-IDEMP-0: exactly one invoice survives both consumes.
                Assert.Single(invoices);
                var invoice = invoices[0];
                Assert.Equal(TenantId, invoice.TenantId);
                Assert.Equal(2, invoice.TotalOrders);

                var assignedPays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.EmployeeId == _employeeId && p.PayPeriodId == _payPeriodId)
                    .ToListAsync();

                Assert.Equal(2, assignedPays.Count);
                Assert.All(assignedPays, p => Assert.Equal(invoice.Id, p.EmployeeInvoiceId));
                Assert.All(assignedPays, p => Assert.Equal(TenantId, p.TenantId));
            });
    }

    private static GenerateInvoiceHandler NewConsumer(IServiceProvider provider) => new(
        provider.GetRequiredService<IMediator>(),
        provider.GetRequiredService<IEmployeeRepository>(),
        provider.GetRequiredService<ITenantProvider>(),
        NullLogger<GenerateInvoiceHandler>.Instance);

    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string Enveloped(string employeeId, string payPeriodId, string tenantId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<GenerateInvoiceMessage>(
                MessageKeys.Invoice(payPeriodId, employeeId),
                tenantId,
                new GenerateInvoiceMessage(employeeId, payPeriodId, "en")),
            Json);

    private static async Task SeedEmployeeWithUnpaidPays(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        country.TenantId = TenantId;

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        currency.TenantId = TenantId;

        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var user = User.CreateWithPassword("payroll-emp@cleansia.test", "12345678Test!", "Emp", "Loyee", UserProfile.Employee);
        user.ConfirmEmail();
        user.TenantId = TenantId;
        context.Users.Add(user);

        var employee = Employee.CreateWithUser(user);
        employee.TenantId = TenantId;
        context.Add(employee);

        var payPeriod = PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        payPeriod.TenantId = TenantId;
        context.Add(payPeriod);

        var orderA = NewOrder(user.Id, "buyer-a@cleansia.test");
        var orderB = NewOrder(user.Id, "buyer-b@cleansia.test");
        context.Add(orderA);
        context.Add(orderB);

        await context.CommitAsync(CancellationToken.None);

        var payA = OrderEmployeePay.Create(orderA.Id, employee.Id, payPeriod.Id, basePay: 600m, totalPay: 600m);
        payA.TenantId = TenantId;
        var payB = OrderEmployeePay.Create(orderB.Id, employee.Id, payPeriod.Id, basePay: 400m, totalPay: 400m);
        payB.TenantId = TenantId;
        context.Add(payA);
        context.Add(payB);

        await context.CommitAsync(CancellationToken.None);

        _employeeId = employee.Id;
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
        order.TenantId = TenantId;
        return order;
    }
}
