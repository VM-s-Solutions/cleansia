using Cleansia.Core.AppServices.Features.CashHeld;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.CashHeld;

/// <summary>
/// Owner ruling 2026-09-28, decision 23, over real Postgres: the daily sweep reads the ledger of the company
/// it runs under, e-mails the cleaner whose cash a close could not set off 40 days ago, stamps the entry the
/// balance started with, and asks nothing on its next run.
/// </summary>
[Collection("PostgresCollection")]
public class RequestCashRemittancesSweepTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string TenantId = TestTenants.Second;
    private const string CountryId = "country-cz-remit";
    private const string CurrencyId = "currency-czk-remit";
    private const string CleanerEmail = "remit-cleaner@cleansia.test";

    private readonly Mock<IEmailService> _email = new();
    private string _collectionId = default!;

    [Fact]
    public async Task Cash_Carried_Past_A_Close_For_Longer_Than_Thirty_Days_Is_Asked_For_Once_And_Stamped()
    {
        _email
            .Setup(e => e.SendCashRemittanceRequestEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("message-id");

        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => _email.Object));
                return Task.CompletedTask;
            },
            arrange: SeedAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var first = await mediator.Send(new RequestCashRemittances.Command());
                var second = await mediator.Send(new RequestCashRemittances.Command());
                return (First: first.Value!.Requested, Second: second.Value!.Requested);
            },
            assert: async (CleansiaDbContext context, (int First, int Second) requested) =>
            {
                Assert.Equal((1, 0), requested);
                var collection = await context.CashLedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.Id == _collectionId);
                Assert.NotNull(collection.RemittanceRequestedAt);
                _email.Verify(e => e.SendCashRemittanceRequestEmailAsync(
                    CleanerEmail, "Emp Loyee", 500m, "Kč", It.IsAny<DateTime>(), "en", It.IsAny<CancellationToken>()), Times.Once);
            });
    }

    private async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var user = User.CreateWithPassword(CleanerEmail, "12345678Test!", "Emp", "Loyee", UserProfile.Employee, languageCode: "en");
        user.ConfirmEmail();
        user.TenantId = TenantId;
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        employee.TenantId = TenantId;
        context.Add(employee);
        await context.CommitAsync(CancellationToken.None);

        var closedAt = DateTime.UtcNow.AddDays(-40);
        var period = PayPeriod.Create(DateOnly.FromDateTime(closedAt.AddDays(-15)), DateOnly.FromDateTime(closedAt.AddDays(-1)))
            .Close("System");
        SetPrivate(period, nameof(PayPeriod.ClosedAt), closedAt);
        period.TenantId = TenantId;
        context.Add(period);

        var order = Order.Create(
            customerName: "Cash Customer",
            customerEmail: "cash-customer@cleansia.test",
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Order St 9", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-50),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: user.Id);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.TenantId = TenantId;
        order.MarkCashCollected(employee.Id, DateTime.UtcNow.AddDays(-50), 1500m);
        context.Add(order);

        var invoice = EmployeeInvoice.Create(employee.Id, period.Id, 1, 1000m, CurrencyId, "2026000001", "INV-2026-000001");
        SetPrivate(invoice, nameof(EmployeeInvoice.GeneratedAt), closedAt.AddMinutes(1));
        invoice.TenantId = TenantId;
        invoice.SetOffCash(1500m);
        context.Add(invoice);

        var collection = CashLedgerEntry.ForCollection(order);
        collection.TenantId = TenantId;
        var setOff = CashLedgerEntry.ForSetOff(invoice);
        setOff.TenantId = TenantId;
        context.CashLedgerEntries.AddRange(collection, setOff);
        _collectionId = collection.Id;
        await context.CommitAsync(CancellationToken.None);
    }

    private static void SetPrivate(object entity, string property, object value) =>
        entity.GetType().GetProperty(property)!.GetSetMethod(nonPublic: true)!.Invoke(entity, [value]);
}
