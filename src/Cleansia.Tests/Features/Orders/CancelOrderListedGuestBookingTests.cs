using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
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
/// Owner ruling 2026-10-26, "Yes, can be cancelled": the signed-in customer whose e-mail a booking made without an
/// account carries cancels it from My orders on the booking's own guest terms, exactly as its e-mailed link does —
/// the refund claimed on refund:{id}:cancel before the cancel, no apology credit, every link it held retired and
/// the cancellation e-mail carrying a fresh one — and never through the no-cleaner body, which pays an account.
/// The booking stays a guest booking. No other account, no other address and no staff session reaches it.
/// </summary>
public sealed class CancelOrderListedGuestBookingTests
{
    private const string OrderId = "order-listed-cancel";
    private const string UserId = "user-listed-cancel";
    private const string Email = "jana.novak@cleansia.test";
    private const string TypedReason = "Booked it twice by mistake.";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<IRefundRepository> _refundRows = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _tokens = new();
    private readonly Mock<ICancellationPolicyResolver> _policy = new();
    private readonly AuditContext _audit = new();
    private readonly List<(string Queue, string Key, object Message)> _enqueued = [];
    private readonly GuestOrderAccessToken _liveLink = GuestOrderAccessToken.Issue(OrderId, DateTimeOffset.UtcNow.AddDays(30));

    public CancelOrderListedGuestBookingTests()
    {
        AsSession(UserProfile.Customer);
        _policy.Setup(r => r.ResolveForOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(
                FreeCancellationHours: BookingPolicy.FreeCancellationHours,
                PartialCancellationHours: BookingPolicy.PartialCancellationHours,
                PartialCancellationFeeRate: BookingPolicy.PartialCancellationFeeRate,
                LastMinuteCancellationFeeRate: BookingPolicy.LastMinuteCancellationFeeRate,
                OopsWindowMinutes: BookingPolicy.OopsWindowMinutesStandard,
                OopsWindowRule: OopsWindowRule.Standard));
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest request, CancellationToken _) => BusinessResult.Success(new RefundResult(
                "refund-1", Cleansia.Core.AppServices.Services.RefundService.BuildRefundKey(request), request.Amount,
                RefundStatus.Succeeded, false)));
        _tokens.Setup(t => t.GetLiveForOrderIgnoringTenantAsync(OrderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_liveLink]);
    }

    [Fact]
    public async Task A_Listed_Guest_Booking_Is_Cancelled_On_Its_Guest_Terms_And_Its_Links_Give_Way_To_The_Cancellation_Email()
    {
        var order = ArrangeOrder(userId: null, Email.ToUpperInvariant(), cleaningInHours: 48);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0m, result.Value!.FeeRate);
        Assert.Equal(1000m, result.Value.ActualRefundAmount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CancelledBy.Customer, order.CancelledBy);
        Assert.Equal(TypedReason, order.CancellationReason);
        Assert.Null(order.CustomerCancellationReason);
        Assert.Null(order.UserId);

        var refund = (RefundRequest)Assert.Single(_refunds.Invocations).Arguments[0];
        Assert.Equal(RefundReason.CustomerCancellation, refund.Reason);
        Assert.Equal($"refund:{OrderId}:cancel", Cleansia.Core.AppServices.Services.RefundService.BuildRefundKey(refund));
        Assert.NotNull(_liveLink.RevokedOn);

        var (queue, key, message) = Assert.Single(_enqueued);
        Assert.Equal(QueueNames.SendEmail, queue);
        Assert.Equal(MessageKeys.GuestOrderCancelledEmail(OrderId), key);
        var email = Assert.IsType<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(message).Payload;
        Assert.Equal(1000m, email.SuccessfulRefundAmount);
        Assert.Null(email.ApologyCreditAmount);

        Assert.NotNull(_audit.DrainSnapshot());
        _credit.Verify(c => c.EnsureForUserIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _receivables.Verify(r => r.Add(It.IsAny<Receivable>()), Times.Never);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Past its start with nobody on it, a booking made without an account keeps its guest terms: the account
    /// reaching it is not its owner, so nobody is paid an apology and the refund stays on the link's key.
    /// </summary>
    [Fact]
    public async Task Past_Its_Start_With_Nobody_On_It_A_Listed_Guest_Booking_Keeps_Its_Guest_Terms_And_Pays_No_Apology()
    {
        var order = ArrangeOrder(userId: null, Email, cleaningInHours: -10.0 / 60);

        var result = await CancelAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TypedReason, order.CancellationReason);
        var refund = (RefundRequest)Assert.Single(_refunds.Invocations).Arguments[0];
        Assert.Equal(RefundReason.CustomerCancellation, refund.Reason);
        _credit.Verify(c => c.EnsureForUserIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var email = Assert.IsType<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(Assert.Single(_enqueued).Message).Payload;
        Assert.Null(email.ApologyCreditAmount);
    }

    [Fact]
    public async Task A_Stripe_Transport_Fault_Escapes_So_The_Cancel_Can_Be_Retried_And_Nothing_Is_Staged()
    {
        var order = ArrangeOrder(userId: null, Email, cleaningInHours: 48);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Stripe unreachable"));

        await Assert.ThrowsAsync<HttpRequestException>(CancelAsync);

        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Empty(_enqueued);
        Assert.Null(_liveLink.RevokedOn);
    }

    [Theory]
    [InlineData("another-account", Email)]
    [InlineData(null, "someone.else@cleansia.test")]
    public async Task A_Booking_The_Account_Neither_Owns_Nor_Is_Listed_For_Is_Not_Found(string? userId, string customerEmail)
    {
        var order = ArrangeOrder(userId, customerEmail, cleaningInHours: 48);

        var result = await CancelAsync();

        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
        _refunds.Verify(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public async Task A_Staff_Session_With_The_Address_Does_Not_Reach_The_Guest_Booking(UserProfile profile)
    {
        AsSession(profile);
        var order = ArrangeOrder(userId: null, Email, cleaningInHours: 48);

        var result = await CancelAsync();

        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
    }

    private void AsSession(UserProfile profile)
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _session.Setup(s => s.GetUserEmail()).Returns(Email);
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role)).Returns(new Claim(ClaimTypes.Role, profile.ToString()));
    }

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
            Mock.Of<ILoyaltyService>(),
            _policy.Object,
            _notifications.Object,
            Mock.Of<ILiveActivityProducer>(),
            ExpressWaiverMocks.NoConsumer().Object,
            new RecordingDispatch(_enqueued),
            _audit,
            TimeProvider.System,
            NullLogger<CustomerOrderCancellation>.Instance),
        NoShowCancellationDoubles.Inert(),
        new GuestOrderAccessTokenIssuer(_tokens.Object),
        new RecordingDispatch(_enqueued));

    private Order ArrangeOrder(string? userId, string customerEmail, double cleaningInHours)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.SetNoShowCredit(250m);
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: customerEmail,
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Vinohradská 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.Created("test", DateTimeOffset.UtcNow.AddDays(-2));
        order.SetCurrency(currency);
        order.AssignStripePaymentIntentId("pi_listed_cancel");
        var track = OrderStatusTrack.Create(OrderStatus.New, order);
        track.Created("test", DateTimeOffset.UtcNow.AddDays(-2));
        order.AddOrderStatus(track);

        var orders = new[] { order }.AsQueryable();
        _orders.Setup(r => r.GetQueryableListedForCustomer(UserId, Email)).Returns(orders.BuildMock());
        _orders.Setup(r => r.GetQueryable()).Returns(orders.BuildMock());
        return order;
    }

    private sealed class RecordingDispatch(List<(string Queue, string Key, object Message)> enqueued) : IPendingDispatch
    {
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            enqueued.Add((queueName, messageKey, message!));

        public IReadOnlyList<PendingMessage> Drain() => [];
    }
}
