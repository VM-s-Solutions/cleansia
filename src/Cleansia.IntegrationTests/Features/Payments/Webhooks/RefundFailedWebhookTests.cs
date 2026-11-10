using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
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
/// Stripe's refund.failed on real Postgres through the real pipeline: an anonymous webhook finds a succeeded
/// refund by its RefundKey metadata across the tenant filter, stamps its failed date and Stripe id under its
/// own company and leaves it succeeded and the order's payment status as it was, and tells that company's
/// administrators once, in the same commit. A redelivery of the event changes nothing.
/// </summary>
[Collection("PostgresCollection")]
public class RefundFailedWebhookTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-refund-bounced";
    private const string RefundKey = "refund:order-refund-bounced:admin:full";
    private const string CurrencyId = "cur-czk-refund-bounced";
    private const string CountryId = "cty-cz-refund-bounced";
    private const string AdminA = "admin-refund-bounced-a";
    private const string AdminB = "admin-refund-bounced-b";

    private static User Administrator(string id, string tenantId)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
        user.Id = id;
        user.TenantId = tenantId;
        user.ConfirmEmail();
        return user;
    }

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);
        context.Users.AddRange(Administrator(AdminA, TestTenants.Default), Administrator(AdminB, TestTenants.Second));

        var order = Order.Create(
            customerName: "Refunded Customer",
            customerEmail: "refund-bounced@cleansia.test",
            customerPhone: "+420777111444",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Refunded,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AssignStripePaymentIntentId("pi_refund_bounced");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        context.Orders.Add(order);

        var refund = Refund.Create(OrderId, RefundKey, 1500m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        refund.MarkSucceeded("re_sent_bounced", DateTimeOffset.UtcNow.AddDays(-1));
        context.Refunds.Add(refund);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_Bounced_Refund_Is_Stamped_Under_Its_Company_Tells_Its_Administrators_Once_And_A_Redelivery_Adds_Nothing()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider =>
            {
                var body = Body("evt_refund_bounced");
                var command = new HandlePaymentNotification.Command(
                    body, StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret));
                BusinessResult first, redelivered;
                await using (var scope = provider.CreateAsyncScope())
                {
                    first = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
                }

                await using (var scope = provider.CreateAsyncScope())
                {
                    redelivered = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
                }

                return (First: first, Redelivered: redelivered);
            },
            assert: async (CleansiaDbContext context, (BusinessResult First, BusinessResult Redelivered) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Redelivered.IsSuccess, results.Redelivered.Error?.Message);

                var refund = await context.Refunds.IgnoreQueryFilters().SingleAsync(r => r.RefundKey == RefundKey);
                Assert.Equal(RefundStatus.Succeeded, refund.Status);
                Assert.NotNull(refund.FailedOn);
                Assert.Equal("re_bounced", refund.StripeRefundId);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);

                var row = Assert.Single(await context.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
                Assert.Equal(AdminNotificationEventCatalog.RefundBounced, row.EventKey);
                Assert.Equal(AdminA, row.UserId);
                Assert.Equal(TestTenants.Default, row.TenantId);
                var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                Assert.Equal(OrderId, args["orderId"]);
                Assert.Equal(order.DisplayOrderNumber, args["orderNumber"]);
                Assert.Equal("1500 CZK", args["amount"]);

                var email = Assert.Single(await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.SendEmail).ToListAsync());
                Assert.Equal(TestTenants.Default, email.TenantId);
                Assert.Equal(
                    MessageKeys.AdminNotificationEmail(AdminNotificationEventCatalog.RefundBounced, refund.Id, $"{AdminA}@cleansia.test"),
                    email.MessageKey);
                Assert.Equal(1, await context.ProcessedStripeEvents.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    private static string Body(string eventId) => $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Core.AppServices.Common.Constants.StripeEventType.RefundFailed}}",
          "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "re_bounced",
              "object": "refund",
              "amount": 150000,
              "currency": "czk",
              "status": "failed",
              "failure_reason": "expired_or_canceled_card",
              "metadata": { "RefundKey": "{{RefundKey}}" }
            },
            "previous_attributes": null
          }
        }
        """;
}
