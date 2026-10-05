using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A platform cancellation refunds the whole sale, so an order already partly refunded gets back the rest of
/// it, through the real refund seam over these repositories: the card share to the card and the credit share
/// to the balance, held to what the sale has left after earlier refunds and complaints settled in credit, and
/// nothing a second time as the credit of an order that ended unrefunded.
/// </summary>
public sealed class PlatformCancellationCreditReturnTests
{
    private const string UserId = "user-platform-credit";
    private const string OrderId = "order-platform-credit";
    private const string PaymentIntentId = "pi_platform_credit";
    private const string RefundKey = $"refund:{OrderId}:cancel";

    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IStripeClient> _stripe = new();

    private PlatformOrderCancellation Cancellation()
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        return new(
            new RefundService(_refunds.Object, _orders.Object, _credit.Object, factory.Object, NullLogger<RefundService>.Instance),
            _refunds.Object,
            _credit.Object,
            Mock.Of<ILoyaltyService>(),
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
            Mock.Of<IPendingDispatch>());
    }

    private Order PartlyRefundedOrder(decimal cardRefunded, decimal creditReturned, decimal settledInCredit)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = "czk";
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
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.PartiallyRefunded,
            userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.ApplyCredit(300m, UserId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        _orders.Setup(o => o.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _refunds.Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardRefunded);
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditReturned);
        _credit.Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settledInCredit);
        return order;
    }

    private Task<PlatformOrderCancellationResult> CancelAsync(Order order) =>
        Cancellation().CancelAsync(order, "admin-1", CancelledBy.Admin, null,
            RefundReason.CustomerCancellation, CancellationToken.None);

    private void VerifyCard(decimal amount)
    {
        _stripe.Verify(s => s.RefundPaymentIntentAsync(PaymentIntentId, amount, RefundKey, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.VerifyNoOtherCalls();
    }

    private void VerifyCreditOnTheRefundKey(decimal amount) =>
        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", amount, $"credit-return:{RefundKey}", "admin-1",
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);

    private void VerifyNoOrderEndedUnpaidReturn() =>
        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), $"credit-return:order-ended-unpaid:{OrderId}",
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);

    /// <summary>
    /// 1000 paid 700 by card and 300 in credit; a partial refund gave back 280 by card and 120 in credit. The
    /// rest, 600, goes back in the mix it was paid in as far as each tender has it: 420 to the card and the
    /// 180 of credit still out.
    /// </summary>
    [Fact]
    public async Task A_Partly_Refunded_Order_Gets_Back_The_Rest_Of_The_Sale_On_Both_Tenders()
    {
        var order = PartlyRefundedOrder(cardRefunded: 280m, creditReturned: 120m, settledInCredit: 0m);

        var result = await CancelAsync(order);

        VerifyCard(420m);
        VerifyCreditOnTheRefundKey(180m);
        VerifyNoOrderEndedUnpaidReturn();
        Assert.Equal(600m, result.RefundAmount);
        Assert.Equal(420m, result.Refund.RefundedAmount);
    }

    /// <summary>
    /// All 300 of the credit already came back on an earlier refund, so the rest of the sale, 300, goes back
    /// to the card only.
    /// </summary>
    [Fact]
    public async Task An_Order_Whose_Credit_Already_All_Came_Back_Takes_The_Rest_On_The_Card_Only()
    {
        var order = PartlyRefundedOrder(cardRefunded: 400m, creditReturned: 300m, settledInCredit: 0m);

        var result = await CancelAsync(order);

        VerifyCard(300m);
        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        Assert.Equal(300m, result.RefundAmount);
    }

    /// <summary>
    /// The worked examples: a partial refund of 400 (280 card, 120 credit) and 200 settled in credit leave 400,
    /// which goes back as 280 card and 120 credit; a partial refund of 800 (560 card, 240 credit) and 150
    /// settled leave 50, which goes back as 35 card and 15 credit. Either way the customer ends with the price.
    /// </summary>
    [Theory]
    [InlineData(280, 120, 200, 280, 120)]
    [InlineData(560, 240, 150, 35, 15)]
    public async Task A_Partly_Refunded_Order_With_A_Settlement_Gets_Back_What_The_Sale_Has_Left_In_Proportion(
        int cardRefunded, int creditReturned, int settledInCredit, int card, int credit)
    {
        var order = PartlyRefundedOrder(cardRefunded, creditReturned, settledInCredit);

        var result = await CancelAsync(order);

        VerifyCard(card);
        VerifyCreditOnTheRefundKey(credit);
        VerifyNoOrderEndedUnpaidReturn();
        Assert.Equal(card + credit, result.RefundAmount);
        Assert.Equal(1000m, cardRefunded + creditReturned + settledInCredit + card + credit);
    }
}
