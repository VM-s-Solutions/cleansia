using System.Text.Json;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// The webhook tells the ORDER's company on real Postgres through the real pipeline. A settled card
/// order writes one <c>admin.order.new</c> row per active administrator of the order's company —
/// none for another company's — and one send-email outbox row per administrator under distinct keys,
/// all in the commit that pays the order; a redelivery of the same Stripe event adds nothing. Two
/// declines on one order write one <c>admin.payment.failed</c> row per administrator and one outbox
/// row per address, and the second webhook commits like the first: the feed read guards the site, so
/// the outbox index is never asked to refuse a repeat.
/// </summary>
[Collection("PostgresCollection")]
public class OrderWebhookTellsAdministratorsTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-webhook-tells-admins";
    private const string PaymentIntentId = "pi_webhook_tells_admins";
    private const string CurrencyId = "currency-czk-webhook-tells";
    private const string CountryId = "country-cz-webhook-tells";
    private const string CustomerId = "user-webhook-tells";
    private const string AdminA1 = "admin-webhook-tells-a1";
    private const string AdminA2 = "admin-webhook-tells-a2";
    private const string AdminB1 = "admin-webhook-tells-b1";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static User Administrator(string id, string tenantId, string? language = null)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, language, adminRole: AdminRole.Administrator);
        user.Id = id;
        user.TenantId = tenantId;
        user.ConfirmEmail();
        return user;
    }

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.AddRange(Language.Create("en", "English"), Language.Create("cs", "Czech"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var customer = User.CreateWithPassword("webhook-tells@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        customer.ConfirmEmail();
        context.Users.Add(customer);
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default, language: "cs"),
            Administrator(AdminA2, TestTenants.Default),
            Administrator(AdminB1, TestTenants.Second));

        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "webhook-tells@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerId);
        order.Id = OrderId;
        order.AssignStripeSessionId("cs_webhook_tells");
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        context.Orders.Add(order);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static HandlePaymentNotification.Command Signed(string body) =>
        new(body, StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret));

    private static Task<List<UserNotification>> AdminRows(CleansiaDbContext context, string eventKey) =>
        context.Set<UserNotification>().IgnoreQueryFilters()
            .Where(n => n.EventKey == eventKey)
            .OrderBy(n => n.UserId)
            .ToListAsync();

    private static Task<List<OutboxMessage>> AdminEmails(CleansiaDbContext context, string eventKey) =>
        context.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.SendEmail && m.Body.Contains(eventKey))
            .OrderBy(m => m.MessageKey)
            .ToListAsync();

    private static QueueEnvelope<SendAdminNotificationEmailMessage> Read(OutboxMessage row) =>
        JsonSerializer.Deserialize<QueueEnvelope<SendAdminNotificationEmailMessage>>(row.Body, Json)!;

    [Fact]
    public async Task A_Paid_Card_Order_Tells_Its_Companys_Administrators_Once_And_A_Redelivery_Adds_Nothing()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var body = StripeWebhookTestPayloads.CheckoutSessionCompletedBody("evt_tells_paid", OrderId, "cs_webhook_tells");
                var first = await mediator.Send(Signed(body));
                var redelivered = await mediator.Send(Signed(body));
                return (First: first, Redelivered: redelivered);
            },
            assert: async (CleansiaDbContext context, (BusinessResult First, BusinessResult Redelivered) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Redelivered.IsSuccess, results.Redelivered.Error?.Message);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);

                var rows = await AdminRows(context, AdminNotificationEventCatalog.OrderNew);
                Assert.Equal([AdminA1, AdminA2], rows.Select(r => r.UserId));
                Assert.All(rows, row =>
                {
                    Assert.Equal(TestTenants.Default, row.TenantId);
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                    Assert.Equal(OrderId, args["orderId"]);
                    Assert.Equal(order.DisplayOrderNumber, args["orderNumber"]);
                    Assert.Equal("1500 Kč", args["amount"]);
                    Assert.Equal(nameof(PaymentType.Card), args["paymentType"]);
                    Assert.Equal(CountryId, args["countryId"]);
                    Assert.Equal(5, args.Count);
                    Assert.DoesNotContain("Jana", row.ArgsJson);
                    Assert.DoesNotContain("@", row.ArgsJson);
                });
                Assert.DoesNotContain(rows, r => r.UserId == AdminB1);

                var emails = await AdminEmails(context, AdminNotificationEventCatalog.OrderNew);
                Assert.Equal(2, emails.Count);
                Assert.Equal(2, emails.Select(e => e.MessageKey).Distinct(StringComparer.Ordinal).Count());
                Assert.Equal(
                    [($"{AdminA1}@cleansia.test", "cs"), ($"{AdminA2}@cleansia.test", "en")],
                    emails.Select(Read).Select(e => (e.Payload.Email, e.Payload.LanguageCode)).OrderBy(e => e.Email));
                Assert.All(emails, e => Assert.Equal(TestTenants.Default, e.TenantId));
            },
            transactional: false);
    }

    [Fact]
    public async Task Two_Declines_On_One_Order_Tell_Its_Administrators_Once_And_Both_Webhooks_Commit()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var first = await mediator.Send(Signed(PaymentIntentFailedBody("evt_tells_declined_1")));
                var second = await mediator.Send(Signed(PaymentIntentFailedBody("evt_tells_declined_2")));
                return (First: first, Second: second);
            },
            assert: async (CleansiaDbContext context, (BusinessResult First, BusinessResult Second) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Second.IsSuccess, results.Second.Error?.Message);
                Assert.Equal(2, await context.ProcessedStripeEvents.IgnoreQueryFilters().CountAsync());

                var rows = await AdminRows(context, AdminNotificationEventCatalog.PaymentFailed);
                Assert.Equal([AdminA1, AdminA2], rows.Select(r => r.UserId));
                Assert.All(rows, row =>
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                    Assert.Equal(OrderId, args["orderId"]);
                    Assert.Equal(2, args.Count);
                });

                var emails = await AdminEmails(context, AdminNotificationEventCatalog.PaymentFailed);
                Assert.Equal(2, emails.Count);
                Assert.Equal(
                    [$"{AdminA1}@cleansia.test", $"{AdminA2}@cleansia.test"],
                    emails.Select(e => Read(e).Payload.Email).OrderBy(e => e));

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
            },
            transactional: false);
    }

    private static string PaymentIntentFailedBody(string eventId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "payment_intent.payment_failed",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "{{PaymentIntentId}}",
              "object": "payment_intent",
              "amount": 150000,
              "currency": "czk",
              "status": "requires_payment_method",
              "metadata": {
                "OrderId": "{{OrderId}}"
              }
            },
            "previous_attributes": null
          }
        }
        """;
    }
}
