using System.Globalization;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Constants = Cleansia.Core.AppServices.Common.Constants;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Q-BROWSE-01, answered (b): the second half. The preferred-offer push deep-links to the order detail,
/// whose gate is now <see cref="OrderAvailability"/>, so the announcement moved off CREATION and onto the
/// two writes that make a not-yet-offerable order offerable — the Stripe webhook's completed session
/// (card) and the customer's confirmation of a recurring cash occurrence.
///
/// <para>The negative case is the one that decides the design: <c>Order.PreferredEmployeeId</c> records
/// what the customer ASKED FOR and is written even when the resolver refused — a muted, unreachable,
/// unapproved or already-busy cleaner — so a deferred announcement read off the column alone would push
/// exactly the cleaners ADR-0036 D4.1 decided not to push.</para>
/// </summary>
public class PreferredOfferDeferredAnnouncementTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string OrderId = "order-deferred-1";
    private const string CustomerUserId = "user-customer-deferred";
    private const string PreferredEmployeeId = "employee-favourite-deferred";
    private const string TenantId = "tenant-deferred";

    private readonly List<ProducedPush> _pushes = [];
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public PreferredOfferDeferredAnnouncementTests()
    {
        _notificationProducer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (userId, eventKey, args, tenantId, subject, _) =>
                    _pushes.Add(new ProducedPush(userId, eventKey, args, tenantId, subject)))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task The_Card_Order_Announces_Its_Reservation_When_The_Money_Lands()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: null);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await CreateWebhookHandler(GrantingResolver()).Handle(
            SettlementCommand("evt_deferred_1"), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The order is offerable at the instant of the announcement, which is the whole claim: the push
        // and the gate now agree, so the deep link opens a screen that answers.
        Assert.True(OrderAvailability.IsOfferable(
            order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId));

        var offer = Assert.Single(_pushes, p => p.EventKey == NotificationEventCatalog.PreferredOffer);
        Assert.Equal(NoPreferredCleanerHold.RecipientUserId, offer.UserId);
        Assert.Equal(OrderId, offer.Args["orderId"]);
        Assert.Equal(order.DisplayOrderNumber, offer.Args["orderNumber"]);
        // The subject carries the reservation ROUND now, not just the order — an order may hold more
        // than one preferred reservation and the two closures would otherwise share a key.
        Assert.StartsWith(OrderId + ":", offer.Subject);
    }

    /// <summary>
    /// Redelivery. Stripe retries on 5xx and socket reset, and the second delivery finds the order
    /// already Paid — the terminal-state short-circuit returns before the announcement, so the cleaner
    /// is told once and not once per retry.
    /// </summary>
    [Fact]
    public async Task A_Redelivered_Settlement_Does_Not_Announce_It_Twice()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: null);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = CreateWebhookHandler(GrantingResolver());
        await handler.Handle(SettlementCommand("evt_deferred_2"), CancellationToken.None);
        await handler.Handle(SettlementCommand("evt_deferred_3"), CancellationToken.None);

        Assert.Single(_pushes, p => p.EventKey == NotificationEventCatalog.PreferredOffer);
    }

    /// <summary>
    /// The column is a record of the customer's wish, not a licence to push. A refused resolver means
    /// the cleaner cannot act on the offer, and that verdict has to survive the deferral or the delay
    /// silently widens the audience the resolver narrowed.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_The_Resolver_Refuses_Is_Not_Announced_To_On_Settlement()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: null);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await CreateWebhookHandler(NoPreferredCleanerHold.Resolver).Handle(
            SettlementCommand("evt_deferred_4"), CancellationToken.None);

        Assert.DoesNotContain(_pushes, p => p.EventKey == NotificationEventCatalog.PreferredOffer);
    }

    /// <summary>
    /// An order with no preference must not pay for the resolver at all — five reads on the webhook's
    /// hot path for a question nobody asked.
    /// </summary>
    [Fact]
    public async Task An_Order_With_No_Preference_Never_Asks_The_Resolver()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: null, preferredEmployeeId: null);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var resolver = new Mock<IPreferredCleanerHoldResolver>();

        await CreateWebhookHandler(resolver.Object).Handle(
            SettlementCommand("evt_deferred_5"), CancellationToken.None);

        resolver.Verify(
            r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.DoesNotContain(_pushes, p => p.EventKey == NotificationEventCatalog.PreferredOffer);
    }

    /// <summary>
    /// The other transition, and the one no webhook reaches: a recurring CASH occurrence has no Stripe
    /// session at all, so without this site its preferred cleaner would never be told anything.
    /// </summary>
    [Fact]
    public async Task The_Recurring_Cash_Confirmation_Announces_The_Reservation()
    {
        var order = ArrangeOrder(PaymentType.Cash, recurringTemplateId: "tmpl-weekly-deferred");
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await ConfirmHandler(GrantingResolver()).Handle(
            new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(OrderAvailability.IsOfferable(
            order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId));

        var offer = Assert.Single(_pushes, p => p.EventKey == NotificationEventCatalog.PreferredOffer);
        Assert.Equal(NoPreferredCleanerHold.RecipientUserId, offer.UserId);
        // The subject carries the reservation ROUND now, not just the order — an order may hold more
        // than one preferred reservation and the two closures would otherwise share a key.
        Assert.StartsWith(OrderId + ":", offer.Subject);
    }

    /// <summary>
    /// The recurring CARD flavour writes no status here — the webhook does — so it must announce
    /// nothing, or a customer opening the payment sheet would summon a cleaner to an unpaid job.
    /// </summary>
    [Fact]
    public async Task The_Recurring_Card_Confirmation_Announces_Nothing()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: "tmpl-weekly-deferred");
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await ConfirmHandler(GrantingResolver()).Handle(
            new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.DoesNotContain(_pushes, p => p.EventKey == NotificationEventCatalog.PreferredOffer);
    }

    private sealed record ProducedPush(
        string UserId,
        string EventKey,
        Dictionary<string, string> Args,
        string? TenantId,
        string? Subject);

    private static IPreferredCleanerHoldResolver GrantingResolver() =>
        NoPreferredCleanerHold.Grants(DateTime.UtcNow.AddHours(2));

    private static Order ArrangeOrder(
        PaymentType paymentType,
        string? recurringTemplateId,
        string? preferredEmployeeId = PreferredEmployeeId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: Core.Domain.Users.Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerUserId,
            preferredEmployeeId: preferredEmployeeId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private ConfirmRecurringOrder.Handler ConfirmHandler(IPreferredCleanerHoldResolver resolver)
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(CustomerUserId);

        var stripeClient = new Mock<Core.Clients.Abstractions.Stripe.IStripeClient>();

        return new ConfirmRecurringOrder.Handler(
            _orderRepository.Object,
            new Mock<IUserRepository>().Object,
            session.Object,
            stripeClient.Object,
            _pending.Object,
            _notificationProducer.Object,
            resolver,
            NullLogger<ConfirmRecurringOrder.Handler>.Instance);
    }

    private HandlePaymentNotification.Handler CreateWebhookHandler(IPreferredCleanerHoldResolver resolver)
    {
        var stripeConfig = new Mock<IStripeConfig>();
        stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);

        var processedEvents = new Mock<IProcessedStripeEventRepository>();
        processedEvents
            .Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var disputes = new Mock<IDisputeRepository>();
        disputes
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);

        return new HandlePaymentNotification.Handler(
            stripeConfig.Object,
            _orderRepository.Object,
            disputes.Object,
            processedEvents.Object,
            new Mock<IStripeSubscriptionWebhookHandler>().Object,
            new Mock<ITenantProvider>().Object,
            _pending.Object,
            _notificationProducer.Object,
            resolver,
            NullLogger<HandlePaymentNotification.Handler>.Instance);
    }

    private static HandlePaymentNotification.Command SettlementCommand(string eventId)
    {
        var payload = SettlementPayload(eventId);
        return new HandlePaymentNotification.Command(payload, SignPayload(payload));
    }

    private static string SettlementPayload(string eventId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Constants.StripeEventType.CompletedSession}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "cs_test_123",
              "object": "checkout.session",
              "payment_status": "paid",
              "metadata": { "OrderId": "{{OrderId}}" }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    private static string SignPayload(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = EventUtility.ComputeSignature(WebhookSecret, timestamp, payload);
        return $"t={timestamp},v1={signature}";
    }
}
