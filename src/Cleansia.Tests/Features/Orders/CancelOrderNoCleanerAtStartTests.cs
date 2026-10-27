using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A signed-in customer cancels, from its booked start, a booking nobody is on that the unfilled sweep would
/// cancel. Owner rulings 2026-10-25 ("Fix", and "It's important to store"): the cancel is paid as the sweep
/// pays — the whole refund on the sweep's key, the apology credit, the no-cleaner push and e-mail — the
/// cancellation keeps the no-cleaner reason key, and the reason the customer typed is kept beside it.
/// Before the start, with a crew, or on a booking the sweep would not cancel, the cancel is today's.
/// </summary>
public sealed class CancelOrderNoCleanerAtStartTests
{
    private const string OrderId = "order-no-cleaner-at-start";
    private const string CustomerId = "customer-no-cleaner-at-start";
    private const string CzkId = "czk";
    private const string TypedReason = "Nobody has taken it and I cannot wait any longer.";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<IRefundRepository> _refundRows = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<ILiveActivityProducer> _liveActivity = new();
    private readonly Mock<IExpressWaiverConsumer> _waiver = ExpressWaiverMocks.NoConsumer();
    private readonly Mock<ILoyaltyService> _loyalty = new();
    private readonly Mock<ICancellationPolicyResolver> _policy = new();
    private readonly AuditContext _audit = new();
    private readonly List<(string Queue, string Key, object Message)> _enqueued = [];
    private readonly Currency _czk;
    private CreditAccount? _account;

    public CancelOrderNoCleanerAtStartTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerId);
        _czk = Currency.Create("CZK", "Kč", "Czech koruna");
        _czk.Id = CzkId;
        _czk.SetNoShowCredit(250m);
        _policy.Setup(r => r.ResolveForOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(
                FreeCancellationHours: BookingPolicy.FreeCancellationHours,
                PartialCancellationHours: BookingPolicy.PartialCancellationHours,
                PartialCancellationFeeRate: BookingPolicy.PartialCancellationFeeRate,
                LastMinuteCancellationFeeRate: BookingPolicy.LastMinuteCancellationFeeRate,
                OopsWindowMinutes: BookingPolicy.OopsWindowMinutesStandard,
                OopsWindowRule: OopsWindowRule.Standard));
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest request, CancellationToken _) => BusinessResult.Success(
                new RefundResult("refund-1", RefundKeyFor(request), request.Amount, RefundStatus.Succeeded, false)));
        _credit.Setup(c => c.EnsureForUserIgnoringTenantAsync(CustomerId, CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _account ??= CreditAccount.Create(CustomerId, CzkId, "system"));
    }

    [Fact]
    public async Task A_Paid_Booking_Nobody_Is_On_Cancelled_After_Its_Start_Is_Paid_As_The_Sweep_Pays_And_Keeps_Both_Reasons()
    {
        var order = ArrangeOrder(startedMinutesAgo: 10);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0m, result.Value!.FeeRate);
        Assert.Equal(1000m, result.Value.RefundAmount);
        Assert.True(result.Value.RefundInitiated);
        Assert.Equal(1000m, result.Value.ActualRefundAmount);
        Assert.False(result.Value.RefundPending);

        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CancelledBy.Customer, order.CancelledBy);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Equal(TypedReason, order.CustomerCancellationReason);
        Assert.Single(order.OrderStatusHistory, s => s.Status == OrderStatus.Cancelled);

        var refund = Assert.Single(_refunds.Invocations).Arguments[0];
        Assert.Equal($"refund:{OrderId}:admin", RefundKeyFor((RefundRequest)refund));
        Assert.Equal(1000m, ((RefundRequest)refund).Amount);

        var apology = Assert.Single(_account!.Transactions);
        Assert.Equal($"cleaner-noshow:{OrderId}", apology.IdempotencyKey);
        Assert.Equal(CreditTransactionReason.CleanerNoShow, apology.Reason);
        Assert.Equal(250m, apology.Amount);

        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderNoCleanerRefunded,
            It.Is<Dictionary<string, string>>(args => args["amount"] == "250 Kč"),
            It.IsAny<string?>(), OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        var email = Assert.IsType<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(Assert.Single(_enqueued).Message).Payload;
        Assert.Equal(1000m, email.SuccessfulRefundAmount);
        Assert.Equal(250m, email.ApologyCreditAmount);
        Assert.True(email.NoCleanerAvailable);

        _waiver.Verify(w => w.ReleaseForOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _liveActivity.Verify(l => l.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, It.IsAny<OrderStatusTrack>(), It.IsAny<CancellationToken>()), Times.Once);
        _loyalty.Verify(l => l.RevokeForCancelledOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);

        var evidence = _audit.DrainSnapshot();
        Assert.NotNull(evidence);
        var payload = JsonDocument.Parse(evidence!.AfterJson!).RootElement;
        Assert.Equal("freeNotAccepted", payload.GetProperty("tier").GetString());
        Assert.Equal(1000m, payload.GetProperty("refundAmount").GetDecimal());
        Assert.True(payload.GetProperty("refundInitiated").GetBoolean());
        Assert.True(payload.GetProperty("reasonProvided").GetBoolean());
        Assert.DoesNotContain(TypedReason, evidence.AfterJson);
    }

    [Fact]
    public async Task A_Cash_Booking_Nobody_Came_To_Is_Told_Nothing_Was_Charged_And_Owes_Nothing()
    {
        var order = ArrangeOrder(startedMinutesAgo: 10, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Equal(0m, result.Value!.RefundAmount);
        _refunds.Verify(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _receivables.Verify(r => r.Add(It.IsAny<Receivable>()), Times.Never);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderNoCleanerNothingCharged,
            It.Is<Dictionary<string, string>>(args => args["amount"] == "250 Kč"),
            It.IsAny<string?>(), OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Refund_Stripe_Refuses_Is_Left_Pending_And_The_Customer_Is_Told_So()
    {
        ArrangeOrder(startedMinutesAgo: 10);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));
        _refundRows.Setup(r => r.GetByRefundKeyAsync($"refund:{OrderId}:admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Refund.Create(
                OrderId, $"refund:{OrderId}:admin", 1000m, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund));

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value!.RefundInitiated);
        Assert.Null(result.Value.ActualRefundAmount);
        Assert.True(result.Value.RefundPending);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderNoCleanerRefundPending,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Currency_With_No_Apology_Figure_Pays_None_And_Sends_The_Plain_Cancellation()
    {
        var euro = Currency.Create("EUR", "€", "Euro");
        euro.Id = CzkId;
        var order = ArrangeOrder(startedMinutesAgo: 10);
        order.SetCurrency(euro);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Null(_account);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderCancelled,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Minute_Before_Its_Start_The_Cancel_Is_Todays_Free_One_With_The_Typed_Reason_And_No_Apology()
    {
        var order = ArrangeOrder(startedMinutesAgo: -1);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TypedReason, order.CancellationReason);
        Assert.Null(order.CustomerCancellationReason);
        Assert.Equal($"refund:{OrderId}:cancel", RefundKeyFor((RefundRequest)Assert.Single(_refunds.Invocations).Arguments[0]));
        Assert.Null(_account);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_enqueued);
    }

    [Fact]
    public async Task Past_Its_Start_With_A_Cleaner_On_It_The_Cancel_Is_Still_Refused()
    {
        var order = ArrangeOrder(startedMinutesAgo: 10, withCrew: true);

        var result = await CancelAsync();

        Assert.Equal(BusinessErrorMessage.OrderStartPassedCannotCancel, result.Error!.Message);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
        _refunds.Verify(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// An unpaid card checkout was never offered to a cleaner, and a partly refunded booking is outside the
    /// sweep's money term, so the sweep cancels neither and nobody failed to come: the cancel is today's.
    /// </summary>
    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.PartiallyRefunded)]
    public async Task Past_Its_Start_A_Booking_The_Sweep_Would_Not_Cancel_Keeps_Todays_Cancel(PaymentStatus paymentStatus)
    {
        var order = ArrangeOrder(startedMinutesAgo: 10, paymentStatus: paymentStatus);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TypedReason, order.CancellationReason);
        Assert.Null(order.CustomerCancellationReason);
        Assert.Null(_account);
        Assert.Empty(_enqueued);
    }

    private static string RefundKeyFor(RefundRequest request) =>
        Cleansia.Core.AppServices.Services.RefundService.BuildRefundKey(request);

    private Task<BusinessResult<CancelOrder.Response>> CancelAsync() =>
        Handler().Handle(new CancelOrder.Command(OrderId, TypedReason), CancellationToken.None);

    private CancelOrder.Handler Handler() => new(
        OrderAccessDoubles.Over(_orders, _session),
        _session.Object,
        new CustomerOrderCancellation(
            Mock.Of<ITenantProvider>(),
            _refunds.Object,
            _refundRows.Object,
            _receivables.Object,
            _credit.Object,
            _loyalty.Object,
            _policy.Object,
            _notifications.Object,
            _liveActivity.Object,
            _waiver.Object,
            new RecordingDispatch(_enqueued),
            _audit,
            TimeProvider.System,
            NullLogger<CustomerOrderCancellation>.Instance),
        new CleanerNoShowCancellation(
            _credit.Object,
            _refunds.Object,
            _refundRows.Object,
            _notifications.Object,
            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
            new RecordingDispatch(_enqueued),
            NullLogger<CleanerNoShowCancellation>.Instance),
        new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
        new RecordingDispatch(_enqueued));

    private Order ArrangeOrder(
        double startedMinutesAgo,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        bool withCrew = false)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana@example.test",
            customerPhone: "+420111000222",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-startedMinutesAgo),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: paymentStatus,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.Created("test", DateTimeOffset.UtcNow.AddDays(-2));
        order.SetCurrency(_czk);
        if (paymentType == PaymentType.Card)
        {
            order.AssignStripePaymentIntentId("pi_no_cleaner_at_start");
        }

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in withCrew ? new[] { OrderStatus.New, OrderStatus.Confirmed } : [OrderStatus.New])
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("test", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        if (withCrew)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee("emp-no-cleaner-at-start", ContractStatus.Approved)));
        }

        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private sealed class RecordingDispatch(List<(string Queue, string Key, object Message)> enqueued) : IPendingDispatch
    {
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            enqueued.Add((queueName, messageKey, message!));

        public IReadOnlyList<PendingMessage> Drain() => [];
    }
}
