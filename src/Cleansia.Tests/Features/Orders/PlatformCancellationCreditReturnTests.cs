using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A platform cancellation refunds the whole sale, so an order already partly refunded gets back the rest of
/// it, through the real refund seam over these repositories: the card share to the card and the credit share
/// to the balance, held to what the sale has left after earlier refunds and complaints settled in credit, and
/// nothing a second time as the credit of an order that ended unrefunded. A refund Stripe refuses gives its
/// credit share back at once, and one a cancellation that never committed already claimed is recorded at what
/// it gives back.
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
    private readonly Mock<IGuestOrderAccessTokenRepository> _guestTokens = new();

    public PlatformCancellationCreditReturnTests()
    {
        _guestTokens.Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuestOrderAccessToken>());
    }

    private RefundService RefundService()
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        return new RefundService(_refunds.Object, _orders.Object, _credit.Object, factory.Object, NullLogger<RefundService>.Instance);
    }

    private PlatformOrderCancellation Cancellation() =>
        new(
            RefundService(),
            _refunds.Object,
            _credit.Object,
            Mock.Of<ILoyaltyService>(),
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            new GuestOrderAccessTokenIssuer(_guestTokens.Object),
            Mock.Of<IPendingDispatch>(),
            NullLogger<PlatformOrderCancellation>.Instance);

    private Order PartlyRefundedOrder(
        decimal cardRefunded,
        decimal creditReturned,
        decimal settledInCredit,
        decimal total = 1000m,
        decimal creditApplied = 300m)
    {
        var order = CardOrder(total, creditApplied, PaymentStatus.PartiallyRefunded, UserId);
        _refunds.Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardRefunded);
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditReturned);
        _credit.Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settledInCredit);
        return order;
    }

    private Order CardOrder(decimal total, decimal creditApplied, PaymentStatus paymentStatus, string? userId)
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
            totalPrice: total,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, userId!);
        }

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        _orders.Setup(o => o.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    /// <summary>
    /// Refund rows and credit returns kept in lists, over the totals the order gave back before this
    /// cancellation, so a claim, a refusal and the re-drive that follows read one another's writes.
    /// </summary>
    private (List<Refund> Rows, List<(string Key, decimal Amount, string Actor)> Credit) ArrangeLedger(
        decimal cardRefundedBefore, decimal creditReturnedBefore)
    {
        var rows = new List<Refund>();
        var credit = new List<(string Key, decimal Amount, string Actor)>();
        _refunds.Setup(r => r.Add(It.IsAny<Refund>())).Callback<Refund>(rows.Add);
        _refunds.Setup(r => r.GetByRefundKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => rows.SingleOrDefault(r => r.RefundKey == key));
        _refunds.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => rows.SingleOrDefault(r => r.Id == id));
        _refunds.Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => cardRefundedBefore + rows.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount));
        _refunds.Setup(r => r.GetPendingRefundTotalForOrderAsync(OrderId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string? exceptRefundKey, CancellationToken _) => rows
                .Where(r => r.Status == RefundStatus.Pending && r.RefundKey != exceptRefundKey)
                .Sum(r => r.Amount));
        _credit.Setup(c => c.TryReturnAsync(UserId, "czk", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()))
            .ReturnsAsync((string _, string _, decimal amount, string key, string actor, CancellationToken _, string? _, string? _) =>
            {
                if (credit.Any(c => c.Key == key))
                {
                    return false;
                }

                credit.Add((key, amount, actor));
                return true;
            });
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => creditReturnedBefore + credit.Sum(c => c.Amount));
        _credit.Setup(c => c.GetReturnedAmountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => credit.Where(c => c.Key == key).Sum(c => c.Amount));
        return (rows, credit);
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

    /// <summary>
    /// 1100 paid 600 by card and 500 in credit; a partial refund of 300 gave back 163.64 by card and 136.36 in
    /// credit, so 800 is left: 436.36 card and 363.64 credit. Stripe refuses the card leg and the hourly re-drive
    /// finishes it. The credit leg comes back at the cancellation on the refund's own key, so the re-drive sends
    /// the card its 436.36 and returns nothing a second way: 800 to the haléř. Read back from the card amount in
    /// proportion, the credit would have come to 363.63.
    /// </summary>
    [Fact]
    public async Task A_Refund_Stripe_Refuses_Returns_Its_Credit_Share_Now_And_The_Redrive_Gives_Back_Exactly_The_Rest()
    {
        var order = PartlyRefundedOrder(
            cardRefunded: 163.64m, creditReturned: 136.36m, settledInCredit: 0m, total: 1100m, creditApplied: 500m);
        var (rows, credit) = ArrangeLedger(cardRefundedBefore: 163.64m, creditReturnedBefore: 136.36m);
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("card network unavailable"))
            .Returns(Task.CompletedTask);

        var result = await CancelAsync(order);
        var claimed = Assert.Single(rows);
        var redriven = await RefundService().RedriveAsync(claimed.Id, "system", CancellationToken.None);

        Assert.False(result.Refund.Initiated);
        Assert.True(redriven.IsSuccess, redriven.Error?.Message);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 436.36m, RefundKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        var returned = Assert.Single(credit);
        Assert.Equal(($"credit-return:{RefundKey}", 363.64m, "admin-1"), returned);
        Assert.Equal(800m, claimed.Amount + returned.Amount);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    /// <summary>
    /// The same order when Stripe cannot be reached: the first call times out after the claim committed the
    /// cancellation. The credit share comes back at the cancellation on the refund's key, as for a refusal, so
    /// the re-drive sends the card its 436.36 and the customer has 800 to the haléř, not 799.99.
    /// </summary>
    [Fact]
    public async Task A_Refund_Stripe_Cannot_Be_Reached_For_Returns_Its_Credit_Share_Now_And_The_Redrive_Gives_Back_Exactly_The_Rest()
    {
        var order = PartlyRefundedOrder(
            cardRefunded: 163.64m, creditReturned: 136.36m, settledInCredit: 0m, total: 1100m, creditApplied: 500m);
        var (rows, credit) = ArrangeLedger(cardRefundedBefore: 163.64m, creditReturnedBefore: 136.36m);
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timed out"))
            .Returns(Task.CompletedTask);

        var result = await CancelAsync(order);
        var claimed = Assert.Single(rows);
        var redriven = await RefundService().RedriveAsync(claimed.Id, "system", CancellationToken.None);

        Assert.True(result.Refund.Attempted);
        Assert.False(result.Refund.Initiated);
        Assert.True(redriven.IsSuccess, redriven.Error?.Message);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 436.36m, RefundKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        var returned = Assert.Single(credit);
        Assert.Equal(($"credit-return:{RefundKey}", 363.64m, "admin-1"), returned);
        Assert.Equal(800m, claimed.Amount + returned.Amount);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    /// <summary>
    /// A guest cancelled 47 hours ahead at a 25% fee: 750 of the 1000 was claimed on the cancellation's key,
    /// and the cancel itself never committed. The administrator cancels the booking, which replays that claim
    /// on the same key for the 750 Stripe may already have paid, so the cancellation records 750, not the
    /// whole price.
    /// </summary>
    [Fact]
    public async Task A_Cancellation_Whose_Refund_Was_Already_Claimed_Records_What_That_Claim_Gives_Back()
    {
        var order = CardOrder(total: 1000m, creditApplied: 0m, PaymentStatus.Paid, userId: null);
        var (rows, _) = ArrangeLedger(cardRefundedBefore: 0m, creditReturnedBefore: 0m);
        rows.Add(Refund.Create(OrderId, RefundKey, 750m, "CZK", RefundReason.CustomerCancellation, RefundSource.AppRefund));

        var result = await CancelAsync(order);

        VerifyCard(750m);
        Assert.Equal(750m, result.Refund.RefundedAmount);
        Assert.Equal(750m, result.RefundAmount);
        Assert.Equal(750m, order.CancellationRefundAmount);
    }
}
