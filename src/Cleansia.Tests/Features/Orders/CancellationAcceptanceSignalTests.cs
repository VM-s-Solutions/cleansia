using System.Globalization;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Cleansia.Tests.Common;
using Moq;
using Stripe;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// T-0525 — what "a cleaner accepted this job" actually means when the cancellation fee is priced.
///
/// <see cref="OrderStatus.Confirmed"/> is a deliberately OVERLOADED status in this domain: it means
/// "payment settled" OR "cleaner assigned", and four writers produce it —
/// <c>TakeOrder</c> (a cleaner really did claim it), <c>HandlePaymentNotification</c> (the Stripe
/// webhook), <c>ConfirmRecurringOrder</c> (cash auto-confirm) and <c>AdminOverrideOrderStatus</c>.
/// Pricing the fee off the status track therefore charged every card customer a 25%/50% cancellation
/// fee for a job no cleaner had ever seen. The acceptance signal is the ASSIGNMENT ROW.
///
/// Where a case needs an order in <c>Confirmed</c>, it gets there by RUNNING THE REAL WRITER — a
/// signed Stripe webhook, the real cash-confirm handler, the real admin override — never by setting a
/// bool, so the suite pins the production wiring rather than the policy function's argument. The
/// assignment-only cases deliberately carry no <c>Confirmed</c> track at all; that is the hole a
/// status-based predicate cannot see.
/// </summary>
public class CancellationAcceptanceSignalTests
{
    private const string OrderId = "order-accept-1";
    private const string UserId = "user-1";
    private const string WebhookSecret = "whsec_test_secret";
    private const decimal TotalPrice = 1000m;

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IStripeSubscriptionWebhookHandler> _subscriptionHandler = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IStripeClient> _stripeClient = new();

    private RefundRequest? _issuedRefund;

    public CancellationAcceptanceSignalTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        // No membership → the real resolver hands the handler the standard absolute 24h window, the
        // production shape for every non-member.
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
            {
                _issuedRefund = req;
                return BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false));
            });

        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _processedEvents
            .Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
    }

    private CancelOrder.Handler CreateCancelHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _refundService.Object,
            _loyaltyService.Object,
            new CancellationPolicyResolver(_membershipRepository.Object),
            _producer.Object,
            _liveActivityProducer.Object,
            _expressWaiverConsumer.Object);

    private HandlePaymentNotification.Handler CreateWebhookHandler() =>
        new(
            _stripeConfig.Object,
            _orderRepository.Object,
            _disputeRepository.Object,
            _processedEvents.Object,
            _subscriptionHandler.Object,
            _tenantProvider.Object,
            _pending.Object,
            _producer.Object,
            NoPreferredCleanerHold.Resolver,
            NullLogger<HandlePaymentNotification.Handler>.Instance);

    private ConfirmRecurringOrder.Handler CreateRecurringConfirmHandler() =>
        new(
            _orderRepository.Object,
            _userRepository.Object,
            _session.Object,
            _stripeClient.Object,
            _pending.Object,
            _producer.Object,
            NoPreferredCleanerHold.Resolver,
            NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private AdminOverrideOrderStatus.Handler CreateAdminOverrideHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            Mock.Of<IAuditContext>(),
            _liveActivityProducer.Object);

    /// <summary>
    /// A brand-new unpaid order booked <paramref name="bookedMinutesAgo"/> ago whose cleaning starts
    /// <paramref name="cleaningInHours"/> from now, carrying only the <c>New</c> track — exactly what
    /// exists before any of the four <c>Confirmed</c> writers runs.
    /// </summary>
    private Order ArrangeNewOrder(
        double cleaningInHours,
        int bookedMinutesAgo = 20,
        PaymentType paymentType = PaymentType.Card,
        string? recurringTemplateId = null)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: paymentType,
            totalPrice: TotalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.Created("tester", DateTime.UtcNow.AddMinutes(-bookedMinutesAgo));
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_accept");

        var track = OrderStatusTrack.Create(OrderStatus.New, order);
        track.Created("tester", DateTimeOffset.UtcNow.AddMinutes(-bookedMinutesAgo));
        order.AddOrderStatus(track);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    private static void AssignCleaner(Order order, string employeeId = "emp-1")
    {
        var user = User.CreateWithPassword($"{employeeId}@cleansia.test", "Passw0rd!", "Clean", "Er");
        user.Id = $"{employeeId}-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
    }

    private async Task ConfirmThroughStripeWebhookAsync(string eventId = "evt_accept_1")
    {
        var payload = CompletedSessionPayload(eventId);
        var result = await CreateWebhookHandler().Handle(
            new HandlePaymentNotification.Command(payload, SignPayload(payload)), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private async Task<CancelOrder.Response> CancelAsync()
    {
        var result = await CreateCancelHandler().Handle(
            new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    // ── AC2: the live defect — a card order the payment webhook confirmed, with no cleaner on it ──

    [Fact]
    public async Task WebhookConfirmed_NoCleanerAssigned_CancelledInsideFreeWindow_IsFree_AndRefundsInFull()
    {
        // The exact reported case: books, pays by card, changes their mind 20 minutes later (past the
        // 15-min oops window) with the cleaning 23.7h away (inside the 24h free window). Before the fix
        // the webhook's Confirmed track made this "accepted" and the customer was billed 25%.
        var order = ArrangeNewOrder(cleaningInHours: 23.7, bookedMinutesAgo: 20);
        await ConfirmThroughStripeWebhookAsync();

        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Empty(order.AssignedEmployees);

        var response = await CancelAsync();

        Assert.Equal(0m, response.FeeRate);
        Assert.Equal(TotalPrice, response.RefundAmount);
        Assert.True(response.RefundInitiated);
        Assert.Equal(TotalPrice, _issuedRefund!.Amount);
        Assert.Equal(0m, order.CancellationFeeRate);
        Assert.Equal(TotalPrice, order.CancellationRefundAmount);
    }

    [Fact]
    public async Task WebhookConfirmed_NoCleanerAssigned_CancelledInsideLastMinuteTier_IsStillFree()
    {
        // 1h before start is the 50% tier. Nobody was ever dispatched, so there is no cleaner's time to
        // compensate — the tier must not fire at all.
        var order = ArrangeNewOrder(cleaningInHours: 1);
        await ConfirmThroughStripeWebhookAsync("evt_accept_2");

        var response = await CancelAsync();

        Assert.Equal(0m, response.FeeRate);
        Assert.Equal(TotalPrice, response.RefundAmount);
        Assert.Empty(order.AssignedEmployees);
    }

    // ── AC4: cash auto-confirm ──

    [Fact]
    public async Task CashAutoConfirmed_NoCleanerAssigned_IsFree()
    {
        var order = ArrangeNewOrder(
            cleaningInHours: 1, paymentType: PaymentType.Cash, recurringTemplateId: "tpl-1");

        var confirm = await CreateRecurringConfirmHandler().Handle(
            new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(confirm.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);

        var response = await CancelAsync();

        Assert.Equal(0m, response.FeeRate);
        Assert.Equal(TotalPrice, response.RefundAmount);
    }

    // ── AC5: an admin walking the lifecycle forward ──

    [Fact]
    public async Task AdminOverriddenToConfirmed_NoCleanerAssigned_IsFree()
    {
        var order = ArrangeNewOrder(cleaningInHours: 12);

        var overridden = await CreateAdminOverrideHandler().Handle(
            new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.Confirmed), CancellationToken.None);

        Assert.True(overridden.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);

        var response = await CancelAsync();

        Assert.Equal(0m, response.FeeRate);
        Assert.Equal(TotalPrice, response.RefundAmount);
    }

    // ── AC3: a real acceptance still charges, at the right tier ──

    [Fact]
    public async Task CleanerAssigned_CancelledInPartialTier_Charges25Percent()
    {
        // 12h before start → [4, 24) → 25%. Hand-derived refund: 1000 × (1 − 0.25) = 750.
        var order = ArrangeNewOrder(cleaningInHours: 12);
        await ConfirmThroughStripeWebhookAsync("evt_accept_3");
        AssignCleaner(order);

        var response = await CancelAsync();

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, response.FeeRate);
        Assert.Equal(750m, response.RefundAmount);
        Assert.Equal(750m, _issuedRefund!.Amount);
    }

    [Fact]
    public async Task CleanerAssigned_CancelledInLastMinuteTier_Charges50Percent()
    {
        // 1h before start → < 4h → 50%. Hand-derived refund: 1000 × (1 − 0.50) = 500.
        var order = ArrangeNewOrder(cleaningInHours: 1);
        await ConfirmThroughStripeWebhookAsync("evt_accept_4");
        AssignCleaner(order);

        var response = await CancelAsync();

        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, response.FeeRate);
        Assert.Equal(500m, response.RefundAmount);
        Assert.Equal(500m, _issuedRefund!.Amount);
    }

    [Fact]
    public async Task CleanerAssigned_CancelledOutsideFreeWindow_IsStillFree()
    {
        // The fee ladder is untouched by the fix: an accepted job cancelled 48h out is free on TIMING,
        // not on acceptance.
        var order = ArrangeNewOrder(cleaningInHours: 48);
        await ConfirmThroughStripeWebhookAsync("evt_accept_5");
        AssignCleaner(order);

        var response = await CancelAsync();

        Assert.Equal(0m, response.FeeRate);
        Assert.Equal(TotalPrice, response.RefundAmount);
    }

    [Fact]
    public async Task CleanerAssigned_WithoutAnyConfirmedTrack_StillCharges()
    {
        // The case a status-based predicate cannot see even in principle: TakeOrder assigns
        // unconditionally but writes its Confirmed track only from New/Pending, so a cleaner taking an
        // order the webhook already moved to Confirmed leaves NO new track. Here the order never gets a
        // Confirmed track at all and the assignment row is the only evidence a cleaner exists.
        var order = ArrangeNewOrder(cleaningInHours: 12);
        AssignCleaner(order);

        Assert.DoesNotContain(order.OrderStatusHistory, s => s.Status == OrderStatus.Confirmed);

        var response = await CancelAsync();

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, response.FeeRate);
        Assert.Equal(750m, response.RefundAmount);
    }

    [Fact]
    public async Task MultiCleanerOrder_AnyAssignment_Charges()
    {
        // MaxEmployees > 1 orders carry several assignment rows; any row ≥ 1 means a cleaner was pulled
        // onto the job, which is what the fee prices.
        var order = ArrangeNewOrder(cleaningInHours: 12);
        order.SetMaxEmployees(3);
        AssignCleaner(order, "emp-a");
        AssignCleaner(order, "emp-b");

        var response = await CancelAsync();

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, response.FeeRate);
    }

    // ── The oops window still applies once a cleaner IS on the job ──

    [Fact]
    public async Task CleanerAssigned_CancelledInsideOopsWindow_IsFree()
    {
        // Strictly inside the 15-min cap: the handler reads DateTime.UtcNow, so booking exactly at the
        // cap would land microseconds past it. The exact boundary is pinned deterministically by the
        // pure-function suite (CancellationFeeRateBoundaryTests).
        var order = ArrangeNewOrder(cleaningInHours: 3, bookedMinutesAgo: 10);
        AssignCleaner(order);

        var response = await CancelAsync();

        Assert.Equal(0m, response.FeeRate);
    }

    private static string CompletedSessionPayload(string eventId)
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
              "id": "cs_test_accept",
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
