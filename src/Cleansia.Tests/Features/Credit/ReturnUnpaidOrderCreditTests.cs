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
/// on the order already settled in credit has given part of the sale back, so that much less is returned
/// and the order never gives back more than the credit it took.
/// </summary>
public sealed class ReturnUnpaidOrderCreditTests
{
    private const string UserId = "user-unpaid-credit";
    private const string OrderId = "order-unpaid-credit";

    private readonly Mock<ICreditAccountRepository> _credit = new();

    private static Order OrderWithCredit(decimal creditApplied)
    {
        var order = Order.Create(
            customerName: "Credit Customer",
            customerEmail: "credit@example.test",
            customerPhone: "+420111555999",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Cash,
            totalPrice: 2000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
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

        await _credit.Object.ReturnUnpaidOrderCreditAsync(order, "system", CancellationToken.None);

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

        var moved = await _credit.Object.ReturnUnpaidOrderCreditAsync(order, "system", CancellationToken.None);

        Assert.False(moved);
        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }
}
