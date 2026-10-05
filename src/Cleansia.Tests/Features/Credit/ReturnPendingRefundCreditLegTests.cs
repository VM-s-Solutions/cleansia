using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// A refund left pending returns its credit leg at once, on its own key, as the credit share of the slice the
/// seam froze the card row from. Another refund still pending may already have been paid by Stripe, so it
/// counts against what the sale has left; the refund's own just-claimed row does not.
/// </summary>
public sealed class ReturnPendingRefundCreditLegTests
{
    private const string UserId = "user-pending-leg";
    private const string OrderId = "order-pending-leg";

    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IRefundRepository> _refunds = new();

    /// <summary>
    /// 1000 paid 700 by card and 300 in credit, 100 settled in credit, and an admin's refund of 300 still
    /// pending on the card. A cancellation's refund just claimed 420 on its own key and is left pending too.
    /// Its credit share is of the 600 the sale has left beside the other refund: 180. Forgetting the other
    /// refund would return 270; counting its own row would return 54.
    /// </summary>
    [Fact]
    public async Task The_Credit_Leg_Counts_Other_Pending_Refunds_But_Not_Its_Own()
    {
        var order = Order.Create(
            customerName: "Pending Leg",
            customerEmail: "pending-leg@example.test",
            customerPhone: "+420111555777",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.ApplyCredit(300m, UserId);
        var request = new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, UserId);
        var ownKey = RefundService.BuildRefundKey(request);
        _refunds.Setup(r => r.GetPendingRefundTotalForOrderAsync(OrderId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(720m);
        _refunds.Setup(r => r.GetPendingRefundTotalForOrderAsync(OrderId, ownKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(300m);
        _credit.Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        await _credit.Object.ReturnPendingRefundCreditLegAsync(_refunds.Object, order, request, CancellationToken.None);

        _credit.Verify(c => c.TryReturnAsync(
            UserId, "czk", 180m, $"credit-return:{ownKey}", UserId,
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }
}
