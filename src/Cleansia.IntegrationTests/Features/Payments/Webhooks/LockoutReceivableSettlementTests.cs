using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// A cash customer pays a lockout receivable by pay link after the lockout was confirmed. The confirmation
/// asked for the crew's pay at once (owner decision 2026-10-04), so the pay row is already in the outbox,
/// dispatched and kept, under the key a second request would use. Settling the receivable must commit and ask
/// for nothing more; asking again failed the webhook's commit on the outbox's unique key, on every Stripe
/// retry, and the receivable stayed open.
/// </summary>
[Collection("PostgresCollection")]
public class LockoutReceivableSettlementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-lock-settle";
    private const string CountryId = "country-cz-lock-settle";

    private string _orderId = "";
    private string _employeeId = "";
    private string _receivableId = "";

    [Fact]
    public async Task A_Paid_Lockout_Receivable_Commits_And_Asks_For_No_Pay_Beyond_The_Confirmations()
    {
        await TestMethod(
            arrange: SeedConfirmedCashLockout,
            act: provider =>
            {
                var body = StripeWebhookTestPayloads.ReceivablePayLinkCompletedBody(
                    "evt_lockout_pay_link", _receivableId, "pi_lockout_pay_link");
                return provider.GetRequiredService<IMediator>().Send(new HandlePaymentNotification.Command(
                    body, StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret)));
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var receivable = await context.Receivables.IgnoreQueryFilters().SingleAsync(r => r.Id == _receivableId);
                Assert.Equal(ReceivableStatus.Paid, receivable.Status);
                Assert.Equal("pi_lockout_pay_link", receivable.StripePaymentIntentId);

                var pay = await context.OutboxMessages.IgnoreQueryFilters()
                    .SingleAsync(m => m.QueueName == QueueNames.CalculateOrderPay);
                Assert.Equal(MessageKeys.Pay(_orderId, _employeeId), pay.MessageKey);
                Assert.Equal(OutboxMessageStatus.Dispatched, pay.Status);

                Assert.Equal(1, await context.OutboxMessages.IgnoreQueryFilters()
                    .CountAsync(m => m.QueueName == QueueNames.GenerateReceipt
                        && m.MessageKey == MessageKeys.FeeReceipt(_receivableId)));
            });
    }

    private async Task SeedConfirmedCashLockout(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var customer = User.CreateWithPassword("lockout-settle-customer@cleansia.test", "12345678Test!", "Lockout", "Customer");
        context.Users.Add(customer);

        var cleanerUser = User.CreateWithPassword(
            "lockout-settle-cleaner@cleansia.test", "12345678Test!", "Lockout", "Cleaner", UserProfile.Employee);
        cleanerUser.ConfirmEmail();
        cleanerUser.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(cleanerUser);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        context.Set<Employee>().Add(cleaner);

        var order = Order.Create(
            customerName: "Lockout Customer",
            customerEmail: "lockout-settle-customer@cleansia.test",
            customerPhone: "+420777444555",
            customerAddress: Address.Create("Zamcena 5", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-1),
            paymentType: PaymentType.Cash,
            totalPrice: 1800m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: customer.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, feeRate: BookingPolicy.LockoutFeeRate, refundAmount: 0m,
            reason: OrderCancellationReasons.CustomerLockout);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        context.Add(order);

        var receivable = Receivable.ForLockout(order, order.TotalPrice);
        context.Receivables.Add(receivable);

        var confirmationPay = OutboxMessage.Create(
            QueueNames.CalculateOrderPay, MessageKeys.Pay(order.Id, cleaner.Id), "{}", TestTenants.Default);
        confirmationPay.MarkDispatched(DateTimeOffset.UtcNow.AddMinutes(-30));
        context.OutboxMessages.Add(confirmationPay);

        await context.CommitAsync(CancellationToken.None);

        _orderId = order.Id;
        _employeeId = cleaner.Id;
        _receivableId = receivable.Id;
    }
}
