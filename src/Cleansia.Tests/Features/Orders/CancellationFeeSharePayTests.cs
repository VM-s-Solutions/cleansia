using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28, decision 12: the crew is paid half of a late-cancellation fee once it is
/// collected. A card order collects it at cancellation, from the payment it keeps, so each cleaner's pay is
/// asked for then; a free cancellation collects nothing, and a cash order's fee is only owed until its
/// receivable is paid.
/// </summary>
public class CancellationFeeSharePayTests
{
    private const string OrderId = "order-fee-share-1";
    private const string UserId = "user-fee-share-1";
    private const string TenantId = "cleansia-cz";
    private static readonly string[] Crew = ["emp-fee-share-1", "emp-fee-share-2"];

    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IRefundService> _refunds = new();

    public CancellationFeeSharePayTests()
    {
        _refunds
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest request, CancellationToken _) => BusinessResult.Success(new RefundResult(
                "refund-fee-share", $"refund:{request.OrderId}:cancel", request.Amount, RefundStatus.Succeeded, false)));
    }

    [Fact]
    public async Task A_Late_Cancellation_Of_A_Paid_Card_Order_Asks_For_Each_Cleaners_Share()
    {
        var order = ArrangeOrder(DateTime.UtcNow.AddHours(12), PaymentType.Card, PaymentStatus.Paid);

        var result = await CancelAsync(order);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, order.CancellationFeeRate);
        foreach (var employeeId in Crew)
        {
            _pending.Verify(p => p.Enqueue(
                QueueNames.CalculateOrderPay,
                It.Is<QueueEnvelope<CalculateOrderPayMessage>>(e => e.TenantId == TenantId
                    && e.Payload.OrderId == OrderId && e.Payload.EmployeeId == employeeId),
                MessageKeys.Pay(OrderId, employeeId)), Times.Once);
        }
    }

    [Fact]
    public async Task A_Free_Cancellation_Of_A_Paid_Card_Order_Asks_For_No_Share()
    {
        var order = ArrangeOrder(DateTime.UtcNow.AddDays(5), PaymentType.Card, PaymentStatus.Paid);

        var result = await CancelAsync(order);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0m, order.CancellationFeeRate);
        VerifyNoPayAskedFor();
    }

    [Fact]
    public async Task A_Late_Cancellation_Of_A_Cash_Order_Asks_For_No_Share_While_Its_Fee_Is_Owed()
    {
        var order = ArrangeOrder(DateTime.UtcNow.AddHours(12), PaymentType.Cash, PaymentStatus.Pending);

        var result = await CancelAsync(order);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, order.CancellationFeeRate);
        VerifyNoPayAskedFor();
    }

    private void VerifyNoPayAskedFor() =>
        _pending.Verify(p => p.Enqueue(
            QueueNames.CalculateOrderPay, It.IsAny<QueueEnvelope<CalculateOrderPayMessage>>(), It.IsAny<string>()),
            Times.Never);

    private Task<BusinessResult<CancelOrder.Response>> CancelAsync(Order order)
    {
        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(UserId);
        var memberships = new Mock<IUserMembershipRepository>();
        memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var handler = new CancelOrder.Handler(
            OrderAccessDoubles.Over(orders, session),
            session.Object,
            new CustomerOrderCancellation(
                Mock.Of<ITenantProvider>(),
                _refunds.Object,
                Mock.Of<IRefundRepository>(),
                Mock.Of<IReceivableRepository>(),
                Mock.Of<ICreditAccountRepository>(),
                Mock.Of<ILoyaltyService>(),
                new CancellationPolicyResolver(memberships.Object, Mock.Of<IOrderRepository>()),
                Mock.Of<INotificationProducer>(),
                Mock.Of<ILiveActivityProducer>(),
                ExpressWaiverMocks.NoConsumer().Object,
                _pending.Object,
                new AuditContext(),
                TimeProvider.System,
                NullLogger<CustomerOrderCancellation>.Instance));

        return handler.Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);
    }

    private static Order ArrangeOrder(DateTime cleaningUtc, PaymentType paymentType, PaymentStatus paymentStatus)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningUtc,
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: UserId);
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);
        order.UpdateEstimatedTime(240).CalculateRequiredEmployees(spareSeats: 0);
        if (paymentType == PaymentType.Card)
        {
            order.AssignStripeSessionId("cs_fee_share");
        }

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("tester", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        foreach (var employeeId in Crew)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(employeeId, ContractStatus.Approved)));
        }

        return order;
    }
}
