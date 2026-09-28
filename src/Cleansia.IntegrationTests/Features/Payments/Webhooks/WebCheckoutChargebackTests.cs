using System.Text.Json;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// A bank chargeback on a WEB card payment, end to end through the real pipeline over real Postgres.
///
/// <para>The chargeback names only the PaymentIntent, and a web order is paid through a Checkout
/// Session, so nothing tied the two together: the order carried the session id, never the intent, and
/// every web chargeback resolved to no order — no dispute, no administrator told, and the photos that
/// could answer the bank deleted on their usual clock. The session's settlement now records its intent,
/// an order paid before that is found through Stripe's session for the intent, and a claim that still
/// matches nothing is put in front of every company's administrators.</para>
/// </summary>
[Collection("PostgresCollection")]
public class WebCheckoutChargebackTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-web-checkout-cb";
    private const string SessionId = "cs_web_checkout_cb";
    private const string PaymentIntentId = "pi_web_checkout_cb";
    private const string StripeDisputeId = "dp_web_checkout_cb";
    private const string CurrencyId = "currency-czk-web-cb";
    private const string CountryId = "country-cz-web-cb";
    private const string AdminA = "admin-web-cb-a";
    private const string AdminB = "admin-web-cb-b";

    [Fact]
    public async Task A_Chargeback_On_A_Web_Checkout_Order_Creates_The_Dispute()
    {
        await TestMethod(
            setup: StripeFindsNoSession,
            arrange: context => SeedPendingWebOrder(context),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var paid = await mediator.Send(Signed(StripeWebhookTestPayloads.CheckoutSessionCompletedBody(
                    "evt_web_cb_paid", OrderId, SessionId, PaymentIntentId)));
                var chargeback = await mediator.Send(Signed(StripeWebhookTestPayloads.ChargeDisputeCreatedBody(
                    "evt_web_cb_dispute", StripeDisputeId, PaymentIntentId)));
                return (Paid: paid, Chargeback: chargeback);
            },
            assert: async (CleansiaDbContext context, (BusinessResult Paid, BusinessResult Chargeback) results) =>
            {
                Assert.True(results.Paid.IsSuccess, results.Paid.Error?.Message);
                Assert.True(results.Chargeback.IsSuccess, results.Chargeback.Error?.Message);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(PaymentIntentId, order.StripePaymentIntentId);
                Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);

                var dispute = await context.Set<Dispute>().IgnoreQueryFilters().SingleAsync(d => d.OrderId == OrderId);
                Assert.Equal(StripeDisputeId, dispute.StripeDisputeId);
                Assert.Equal(DisputeReason.Chargeback, dispute.Reason);
                Assert.Equal(DisputeStatus.Escalated, dispute.Status);
                Assert.Equal(TestTenants.Default, dispute.TenantId);
            });
    }

    [Fact]
    public async Task A_Chargeback_On_A_Web_Order_Paid_Before_Its_Intent_Was_Recorded_Is_Found_Through_Its_Session()
    {
        await TestMethod(
            setup: services => StripeFindsSessionFor(services, OrderId),
            arrange: context => SeedPendingWebOrder(context, PaymentStatus.Paid),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Signed(StripeWebhookTestPayloads.ChargeDisputeCreatedBody(
                    "evt_web_cb_legacy", StripeDisputeId, PaymentIntentId))),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(PaymentIntentId, order.StripePaymentIntentId);
                var dispute = await context.Set<Dispute>().IgnoreQueryFilters().SingleAsync(d => d.OrderId == OrderId);
                Assert.Equal(StripeDisputeId, dispute.StripeDisputeId);
                Assert.Equal(DisputeStatus.Escalated, dispute.Status);
            });
    }

    [Fact]
    public async Task A_Chargeback_That_Matches_No_Order_Tells_The_Administrators_Of_Every_Company()
    {
        await TestMethod(
            setup: StripeFindsNoSession,
            arrange: context => SeedPendingWebOrder(context),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Signed(StripeWebhookTestPayloads.ChargeDisputeCreatedBody(
                    "evt_web_cb_unmatched", StripeDisputeId, "pi_nobody_carries", amountMinorUnits: 99000))),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.False(await context.Set<Dispute>().IgnoreQueryFilters().AnyAsync());

                var rows = await context.Set<UserNotification>().IgnoreQueryFilters()
                    .Where(n => n.EventKey == AdminNotificationEventCatalog.DisputeChargebackUnmatched)
                    .OrderBy(n => n.UserId)
                    .ToListAsync();
                Assert.Equal([AdminA, AdminB], rows.Select(r => r.UserId));
                Assert.Equal([TestTenants.Default, TestTenants.Second], rows.Select(r => r.TenantId));
                Assert.All(rows, row =>
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                    Assert.Equal("990 CZK", args["amount"]);
                    Assert.Equal(StripeDisputeId, args["stripeDisputeId"]);
                });
            });
    }

    private static HandlePaymentNotification.Command Signed(string body) =>
        new(body, StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret));

    private static Task StripeFindsNoSession(IServiceCollection services) => StripeFindsSessionFor(services, orderId: null);

    private static Task StripeFindsSessionFor(IServiceCollection services, string? orderId)
    {
        var client = new Mock<IStripeClient>();
        client
            .Setup(c => c.FindCheckoutSessionOrderIdAsync(PaymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderId);
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(client.Object);
        services.Replace(ServiceDescriptor.Singleton(factory.Object));
        return Task.CompletedTask;
    }

    private static User Administrator(string id, string tenantId)
    {
        var user = User.CreateWithPassword(
            $"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
        user.Id = id;
        user.TenantId = tenantId;
        user.ConfirmEmail();
        return user;
    }

    private static async Task SeedPendingWebOrder(CleansiaDbContext context, PaymentStatus paymentStatus = PaymentStatus.Pending)
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
            customerName: "Web Payer",
            customerEmail: "web-checkout-cb@cleansia.test",
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Webova 1", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: paymentStatus);
        order.Id = OrderId;
        order.AssignStripeSessionId(SessionId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        context.Orders.Add(order);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }
}
