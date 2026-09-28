using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A cancelled order that never took money records no refund: a cash booking or a card never charged
/// used to store price × (1 − fee) as a refund of money nobody had paid. The fee it carries is instead
/// what the customer still owes, and the admin order detail says so — rate and amount.
/// </summary>
public class UnpaidOrderCancellationMoneyFieldsTests
{
    private const string OrderId = "order-unpaid-cancel-1";
    private const string UserId = "user-unpaid-1";

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    public void Cancelling_An_Order_That_Took_No_Payment_Records_No_Refund(PaymentStatus paymentStatus)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            PaymentType = PaymentType.Cash,
            PaymentStatus = paymentStatus,
            TotalPrice = 1000m,
        });

        order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.25m, refundAmount: 750m, reason: null);

        Assert.Equal(0.25m, order.CancellationFeeRate);
        Assert.Equal(0m, order.CancellationRefundAmount);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.PartiallyRefunded)]
    [InlineData(PaymentStatus.Refunded)]
    public void Cancelling_An_Order_That_Took_A_Payment_Records_The_Refund(PaymentStatus paymentStatus)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            PaymentStatus = paymentStatus,
            TotalPrice = 1000m,
        });

        order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.25m, refundAmount: 750m, reason: null);

        Assert.Equal(750m, order.CancellationRefundAmount);
    }

    [Fact]
    public async Task A_Customer_Cancelling_An_Accepted_Cash_Order_Records_The_Fee_Rate_And_No_Refund()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(UserId);
        var refundService = new Mock<IRefundService>();
        var membershipRepository = new Mock<IUserMembershipRepository>();
        membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var order = ArrangeAcceptedCashOrder(DateTime.UtcNow.AddHours(12));
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var handler = new CancelOrder.Handler(
            OrderAccessDoubles.Over(orderRepository, session),
            session.Object,
            new CustomerOrderCancellation(
                Mock.Of<ITenantProvider>(),
                refundService.Object,
                Mock.Of<IRefundRepository>(),
                Mock.Of<ICreditAccountRepository>(),
                Mock.Of<ILoyaltyService>(),
                new CancellationPolicyResolver(membershipRepository.Object),
                Mock.Of<INotificationProducer>(),
                Mock.Of<ILiveActivityProducer>(),
                ExpressWaiverMocks.NoConsumer().Object,
                new AuditContext(),
                TimeProvider.System));

        var result = await handler.Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, order.CancellationFeeRate);
        Assert.Equal(0m, order.CancellationRefundAmount);
        refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, 0.25, 250)]
    [InlineData(PaymentStatus.Pending, 0.5, 500)]
    [InlineData(PaymentStatus.Refunded, 0.25, 0)]
    [InlineData(PaymentStatus.PartiallyRefunded, 0.5, 0)]
    public async Task The_Admin_Detail_Carries_The_Fee_Rate_And_The_Fee_Still_Owed(
        PaymentStatus paymentStatusAtRead, decimal feeRate, decimal expectedOwed)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OrderId,
            PaymentType = paymentStatusAtRead == PaymentStatus.Pending ? PaymentType.Cash : PaymentType.Card,
            PaymentStatus = paymentStatusAtRead,
            TotalPrice = 1000m,
            CurrentStatus = OrderStatus.Cancelled,
        });
        order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate, refundAmount: 1000m * (1m - feeRate), reason: null);

        var detail = await ReadDetailAs(order, UserProfile.Administrator);

        Assert.Equal(feeRate, detail.CancellationFeeRate);
        Assert.Equal(expectedOwed, detail.CancellationFeeOwed);
    }

    [Fact]
    public async Task An_Order_That_Is_Not_Cancelled_Carries_No_Fee_Fields()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = OrderId });

        var detail = await ReadDetailAs(order, UserProfile.Administrator);

        Assert.Null(detail.CancellationFeeRate);
        Assert.Null(detail.CancellationFeeOwed);
    }

    [Fact]
    public async Task A_Customer_Reading_Their_Own_Cancelled_Order_Gets_No_Fee_Fields()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OrderId,
            PaymentType = PaymentType.Cash,
            PaymentStatus = PaymentStatus.Pending,
            CurrentStatus = OrderStatus.Cancelled,
        });
        order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.5m, refundAmount: 500m, reason: null);

        var detail = await ReadDetailAs(order, UserProfile.Customer);

        Assert.Null(detail.CancellationFeeRate);
        Assert.Null(detail.CancellationFeeOwed);
    }

    private static async Task<Core.AppServices.Features.Orders.DTOs.OrderItem> ReadDetailAs(Order order, UserProfile role)
    {
        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.LoadOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        access.Setup(a => a.CanBrowseOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(a => a.CanAccessOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(a => a.IsCustomerCaller()).Returns(role == UserProfile.Customer);
        var session = new Mock<IUserSessionProvider>();
        session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
        var acceptances = new Mock<IWorkContractAcceptanceRepository>();
        acceptances
            .Setup(r => r.GetForSeatsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetOrderDetails.Handler(
            access.Object,
            session.Object,
            Mock.Of<IEmployeePayConfigRepository>(),
            Mock.Of<IOrderEmployeePayRepository>(),
            Mock.Of<IOrderPhotoRepository>(),
            Mock.Of<IEmployeeRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<ITenantRepository>(),
            ExpressWaiverMocks.NoConsumer().Object,
            Mock.Of<IUserMembershipRepository>(),
            acceptances.Object,
            Mock.Of<IEmployeeActionAuditRepository>());

        var result = await handler.Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private static Order ArrangeAcceptedCashOrder(DateTime cleaningUtc)
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
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId);
        order.Id = OrderId;
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("tester", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        var cleanerUser = User.CreateWithPassword("cleaner-cash@cleansia.test", "Passw0rd!", "Clean", "Er");
        cleanerUser.Id = "emp-cash-user";
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = "emp-cash";
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        return order;
    }
}
