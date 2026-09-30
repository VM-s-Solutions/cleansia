using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// An admin's cancellation and a company's wind-down both go through <see cref="PlatformOrderCancellation"/>,
/// which used to tell only the cleaners. A guest booking cancelled there now gets its e-mail: the old links
/// retired, the guest's own language, and the refund the card actually got back — never the one asked for.
/// </summary>
public sealed class PlatformCancellationGuestEmailTests
{
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _guestTokens = new();
    private readonly List<(string Queue, string Key, object Message)> _enqueued = [];

    public PlatformCancellationGuestEmailTests()
    {
        _guestTokens.Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuestOrderAccessToken>());
    }

    [Fact]
    public async Task An_Admin_Cancelled_Guest_Booking_Is_Emailed_With_The_Refund_That_Went_Through()
    {
        var order = CardOrder(userId: null);
        order.SetLanguage("uk");
        var oldLink = GuestOrderAccessToken.Issue(order.Id, DateTimeOffset.UtcNow.AddDays(3));
        _guestTokens.Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                order.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldLink]);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(
                new RefundResult("refund-1", "refund:key", 800m, RefundStatus.Succeeded, false)));

        await Cancellation().CancelAsync(order, "admin-1", CancelledBy.Admin, "Our mistake",
            RefundReason.CustomerCancellation, CancellationToken.None);

        Assert.NotNull(oldLink.RevokedOn);
        var email = SingleGuestEmail(order);
        Assert.Equal("uk", email.LanguageCode);
        Assert.Equal(800m, email.SuccessfulRefundAmount);
    }

    [Fact]
    public async Task A_Guest_Whose_Refund_Stripe_Refused_Is_Not_Told_They_Were_Refunded()
    {
        var order = CardOrder(userId: null);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        await Cancellation().CancelAsync(order, "admin-1", CancelledBy.Admin, null,
            RefundReason.CustomerCancellation, CancellationToken.None);

        Assert.Null(SingleGuestEmail(order).SuccessfulRefundAmount);
    }

    [Fact]
    public async Task A_Guest_Cash_Booking_Closed_By_A_Wind_Down_Is_Emailed_With_No_Refund_Claimed()
    {
        var order = CashOrder();

        await Cancellation().CancelAsync(order, "admin-1", CancelledBy.System, OrderCancellationReasons.CompanyWindDown,
            RefundReason.ServiceNotRendered, CancellationToken.None);

        var email = SingleGuestEmail(order);
        Assert.Null(email.SuccessfulRefundAmount);
        Assert.Equal(Constants.Language.English, email.LanguageCode);
        _refunds.Verify(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Account_Booking_Gets_No_Guest_Email()
    {
        var order = CardOrder(userId: "user-account");
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(
                new RefundResult("refund-1", "refund:key", 1000m, RefundStatus.Succeeded, false)));

        await Cancellation().CancelAsync(order, "admin-1", CancelledBy.Admin, null,
            RefundReason.CustomerCancellation, CancellationToken.None);

        Assert.Empty(_enqueued);
    }

    private SendGuestOrderCancellationEmailMessage SingleGuestEmail(Order order)
    {
        var (queue, key, message) = Assert.Single(_enqueued);
        Assert.Equal(QueueNames.SendEmail, queue);
        Assert.Equal(MessageKeys.GuestOrderCancelledEmail(order.Id), key);
        var email = Assert.IsType<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(message).Payload;
        Assert.Equal(order.Id, email.OrderId);
        return email;
    }

    private PlatformOrderCancellation Cancellation() =>
        new(
            _refunds.Object,
            Mock.Of<ICreditAccountRepository>(),
            Mock.Of<ILoyaltyService>(),
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            new GuestOrderAccessTokenIssuer(_guestTokens.Object),
            new RecordingDispatch(_enqueued));

    private static Order CardOrder(string? userId)
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.Paid, userId);
        order.AssignStripePaymentIntentId("pi_platform_cancel");
        return order;
    }

    private static Order CashOrder() => NewOrder(PaymentType.Cash, PaymentStatus.Pending, userId: null);

    private static Order NewOrder(PaymentType paymentType, PaymentStatus paymentStatus, string? userId)
    {
        var order = Order.Create(
            customerName: "Guest Customer",
            customerEmail: "guest@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus,
            userId: userId);
        order.Id = $"order-platform-{paymentType}-{userId ?? "guest"}";
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private sealed class RecordingDispatch(List<(string Queue, string Key, object Message)> enqueued) : IPendingDispatch
    {
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            enqueued.Add((queueName, messageKey, message!));

        public IReadOnlyList<PendingMessage> Drain() => [];
    }
}
