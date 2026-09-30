using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A platform cancellation gives back the credit a customer applied at checkout — only what has not
/// already come back. A partly refunded order did take money, and its earlier refund's credit leg must
/// not be paid a second time on top of the whole applied amount.
/// </summary>
public sealed class PlatformCancellationCreditReturnTests
{
    private const string UserId = "user-platform-credit";

    private readonly Mock<ICreditAccountRepository> _credit = new();

    private PlatformOrderCancellation Cancellation() =>
        new(
            Mock.Of<IRefundService>(),
            _credit.Object,
            Mock.Of<ILoyaltyService>(),
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
            Mock.Of<IPendingDispatch>());

    private static Order CardOrder(PaymentStatus paymentStatus)
    {
        var order = Order.Create(
            customerName: "Credit Customer",
            customerEmail: "credit@example.test",
            customerPhone: "+420111555999",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus,
            userId: UserId);
        order.Id = $"order-platform-credit-{paymentStatus}";
        order.AssignStripePaymentIntentId("pi_platform_credit");
        order.ApplyCredit(300m, UserId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private void VerifyReturned(Order order, decimal amount) =>
        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", amount, $"credit-return:order-ended-unpaid:{order.Id}", "admin-1",
            It.IsAny<CancellationToken>(), order.Id, It.IsAny<string?>()), Times.Once);

    [Fact]
    public async Task A_Partly_Refunded_Order_Gets_Back_Only_The_Credit_Still_Outstanding()
    {
        var order = CardOrder(PaymentStatus.PartiallyRefunded);
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(120m);

        await Cancellation().CancelAsync(order, "admin-1", CancelledBy.Admin, null,
            RefundReason.CustomerCancellation, CancellationToken.None);

        VerifyReturned(order, 180m);
    }

    [Fact]
    public async Task An_Order_Whose_Credit_Already_All_Came_Back_Returns_Nothing_More()
    {
        var order = CardOrder(PaymentStatus.PartiallyRefunded);
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(300m);

        await Cancellation().CancelAsync(order, "admin-1", CancelledBy.Admin, null,
            RefundReason.CustomerCancellation, CancellationToken.None);

        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }
}
