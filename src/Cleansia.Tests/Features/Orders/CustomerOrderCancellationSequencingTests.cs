using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using Moq;

namespace Cleansia.Tests.Features.Orders;

public class CustomerOrderCancellationSequencingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Guest_refund_precedes_cancel_staging_while_signed_in_sequence_is_preserved(bool guest)
    {
        var order = Order.Create("Customer", "customer@example.test", "+420777111222",
            Address.Create("Street", "Prague", "11000", "CZ"), 1, 1, DateTime.UtcNow.AddDays(2),
            PaymentType.Card, 1000m, "CZK", PaymentStatus.Paid, userId: guest ? null : "account");
        order.TenantId = "operator";
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AssignStripePaymentIntentId("pi_order");
        var refunds = new Mock<IRefundService>();
        refunds.Setup(x => x.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((RefundRequest request, CancellationToken _) =>
            {
                Assert.Equal(guest ? OrderStatus.New : OrderStatus.Cancelled, order.CurrentStatus);
                Assert.Equal(guest, order.CancelledAt is null);
                Assert.Equal(RefundReason.CustomerCancellation, request.Reason);
                return Task.FromResult(BusinessResult.Success(new RefundResult("refund", "key", 400m, RefundStatus.Succeeded, false)));
            });
        var policy = new Mock<ICancellationPolicyResolver>();
        policy.Setup(x => x.ResolveForUserAsync(order.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(24, 4, .25m, .50m));
        var notices = new Mock<INotificationProducer>();
        var waiver = ExpressWaiverMocks.NoConsumer();
        var service = new CustomerOrderCancellation(Mock.Of<ITenantProvider>(), refunds.Object, Mock.Of<IRefundRepository>(),
            Mock.Of<ICreditAccountRepository>(), Mock.Of<ILoyaltyService>(), policy.Object, notices.Object,
            Mock.Of<ILiveActivityProducer>(), waiver.Object, new AuditContext());

        var result = await service.ExecuteAsync(order, null, guest ? "System" : "account", CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(1000m, result.Response.RefundAmount);
        Assert.Equal(400m, result.Response.ActualRefundAmount);
        Assert.Equal(400m, result.SuccessfulRefundAmount);
        waiver.Verify(x => x.ReleaseForOrderAsync(order.Id, It.IsAny<CancellationToken>()), Times.Once);
        policy.Verify(x => x.ResolveForUserAsync(order.UserId, It.IsAny<CancellationToken>()), Times.Once);
        if (guest) Assert.Empty(notices.Invocations);
    }
}
