using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// The credit an order took comes back when the order ends without a card refund of its own. A complaint
/// on the order already settled in credit has given part of the sale back. On an order that took no payment
/// only the credit was paid, so the settlement comes off the credit in full; on one that took a card payment
/// the credit is held to what the sale has left after the card refunds, the credit already returned and the
/// settlement.
/// </summary>
public sealed class ReturnUnpaidOrderCreditTests
{
    private const string UserId = "user-unpaid-credit";
    private const string OrderId = "order-unpaid-credit";

    private readonly Mock<ICreditAccountRepository> _credit = new();

    private static Order OrderWithCredit(decimal creditApplied) =>
        NewOrder(PaymentType.Cash, PaymentStatus.Pending, totalPrice: 2000m, creditApplied);

    private static Order NewOrder(
        PaymentType paymentType, PaymentStatus paymentStatus, decimal totalPrice, decimal creditApplied)
    {
        var order = Order.Create(
            customerName: "Credit Customer",
            customerEmail: "credit@example.test",
            customerPhone: "+420111555999",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: paymentType,
            totalPrice: totalPrice,
            currencyId: "czk",
            paymentStatus: paymentStatus,
            userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.ApplyCredit(creditApplied, UserId);
        return order;
    }

    private void Arrange(decimal returned, decimal settled)
    {
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returned);
        _credit.Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settled);
    }

    [Theory]
    [InlineData(0, 400, 100)]
    [InlineData(100, 300, 100)]
    [InlineData(0, 0, 500)]
    public async Task ReturnUnpaidOrderCredit_NetsAComplaintSettledInCredit(
        decimal returned, decimal settled, decimal expected)
    {
        var order = OrderWithCredit(500m);
        Arrange(returned, settled);

        await _credit.Object.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 0m, "system", CancellationToken.None);

        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", expected, $"credit-return:order-ended-unpaid:{OrderId}", "system",
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(0, 600)]
    [InlineData(200, 300)]
    public async Task ReturnUnpaidOrderCredit_SettlementCoveringTheCredit_ReturnsNothing(decimal returned, decimal settled)
    {
        var order = OrderWithCredit(500m);
        Arrange(returned, settled);

        var moved = await _credit.Object.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 0m, "system", CancellationToken.None);

        Assert.False(moved);
        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// 1000 paid 700 by card and 300 in credit. A partial refund of 400 gave back 280 by card and 120 in
    /// credit, and a complaint was settled in 200 of credit. 400 of the sale is left, so all 180 of the credit
    /// still out comes back: 280 + 120 + 200 + 180 = 780 of the 1000.
    /// </summary>
    [Fact]
    public async Task ReturnUnpaidOrderCredit_CardPaidOrder_ReturnsTheCreditStillOutWhileTheSaleHasThatMuchLeft()
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.PartiallyRefunded, totalPrice: 1000m, creditApplied: 300m);
        Arrange(returned: 120m, settled: 200m);

        await _credit.Object.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 280m, "system", CancellationToken.None);

        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", 180m, $"credit-return:order-ended-unpaid:{OrderId}", "system",
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }

    /// <summary>
    /// The same order with 600 settled in credit: 280 + 120 + 600 is already the whole 1000, so nothing more
    /// comes back.
    /// </summary>
    [Fact]
    public async Task ReturnUnpaidOrderCredit_CardPaidOrder_WhoseSaleIsAllGivenBack_ReturnsNothing()
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.PartiallyRefunded, totalPrice: 1000m, creditApplied: 300m);
        Arrange(returned: 120m, settled: 600m);

        var moved = await _credit.Object.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 280m, "system", CancellationToken.None);

        Assert.False(moved);
        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// A partial refund of 800 gave back 560 by card and 240 in credit, and 150 was settled in credit: 50 of
    /// the sale is left, less than the 60 of credit still out, so 50 comes back and the total is the price.
    /// </summary>
    [Fact]
    public async Task ReturnUnpaidOrderCredit_CardPaidOrder_IsHeldToWhatTheSaleHasLeftAfterItsCardRefunds()
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.PartiallyRefunded, totalPrice: 1000m, creditApplied: 300m);
        Arrange(returned: 240m, settled: 150m);

        await _credit.Object.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 560m, "system", CancellationToken.None);

        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", 50m, $"credit-return:order-ended-unpaid:{OrderId}", "system",
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }

    /// <summary>
    /// A paid order that ends with none of its card refunded here: all 500 of the credit comes back, as the
    /// 1800 the sale has left after the settlement covers it.
    /// </summary>
    [Fact]
    public async Task ReturnUnpaidOrderCredit_PaidCardOrderWithNoCardRefunded_ReturnsAllTheCredit()
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.Paid, totalPrice: 2000m, creditApplied: 500m);
        Arrange(returned: 0m, settled: 200m);

        await _credit.Object.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 0m, "system", CancellationToken.None);

        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", 500m, $"credit-return:order-ended-unpaid:{OrderId}", "system",
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }
}
