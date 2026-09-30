using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28, decision 11: the assigned cleaner reports that they cannot get in no earlier
/// than 15 minutes past the booked start, with an entrance photo and a note of the calls they made. The
/// report stamps the order and tells the company's administrators once; it cancels and charges nothing.
/// </summary>
public class ReportOrderLockoutTests
{
    private const string OrderId = "order-lockout-1";
    private const string EmployeeId = "emp-lockout";
    private const string TenantId = "cleansia-cz";
    private const string CallAttempts = "Called 09:05, 09:10 and 09:15, no answer; rang the bell twice.";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IOrderPhotoRepository> _photos = new();
    private readonly Mock<IOrderAccessService> _access = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();

    public ReportOrderLockoutTests()
    {
        _access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
        _photos.Setup(p => p.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.Entrance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private Order ArrangeOrder(
        OrderStatus status = OrderStatus.InProgress,
        double startedMinutesAgo = 20,
        string assignedEmployeeId = EmployeeId)
    {
        var order = Order.Create(
            customerName: "Locked Out",
            customerEmail: "locked-out@example.test",
            customerPhone: "+420111000333",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-startedMinutesAgo),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "customer-lockout-1");
        order.Id = OrderId;
        order.TenantId = TenantId;

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var track in new[] { OrderStatus.New, status })
        {
            var entry = OrderStatusTrack.Create(track, order);
            entry.Created("test", stamp);
            order.AddOrderStatus(entry);
            stamp = stamp.AddMinutes(1);
        }

        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee(assignedEmployeeId, ContractStatus.Approved)));
        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private Task<BusinessResult<ReportOrderLockout.Response>> ReportAsync() =>
        new ReportOrderLockout.Handler(
                _orders.Object, _photos.Object, _access.Object, _adminNotifier.Object, TimeProvider.System)
            .Handle(new ReportOrderLockout.Command(OrderId, CallAttempts), CancellationToken.None);

    [Fact]
    public async Task The_Report_Stamps_The_Order_And_Alerts_The_Companys_Administrators_Once()
    {
        var order = ArrangeOrder();

        var result = await ReportAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(order.LockoutReportedAt, result.Value!.ReportedAt);
        Assert.Equal(EmployeeId, order.LockoutReportedByEmployeeId);
        Assert.Equal(CallAttempts, order.LockoutCallAttempts);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        Assert.Null(order.CancelledBy);
        _adminNotifier.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Key == AdminNotificationEventCatalog.OrderLockoutReported
                && e.TenantId == TenantId
                && e.Subject == OrderId
                && e.Args["orderId"] == OrderId
                && e.Args["orderNumber"] == order.DisplayOrderNumber
                && e.Args.ContainsKey("cleaningDateTime")
                && e.Args.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Cleaner_Waits_Fifteen_Minutes_Past_The_Start_Before_Reporting()
    {
        var order = ArrangeOrder(startedMinutesAgo: 10);

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.LockoutTooEarly, result.Error!.Message);
        Assert.Null(order.LockoutReportedAt);
        _adminNotifier.Verify(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Report_Without_An_Entrance_Photo_Is_Refused()
    {
        var order = ArrangeOrder();
        _photos.Setup(p => p.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.Entrance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.LockoutPhotoRequired, result.Error!.Message);
        Assert.Null(order.LockoutReportedAt);
    }

    [Fact]
    public async Task A_Second_Report_Is_Refused_And_Alerts_Nobody_Again()
    {
        var order = ArrangeOrder();
        order.ReportLockout(EmployeeId, "First report.", DateTime.UtcNow.AddMinutes(-2));

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.LockoutAlreadyReported, result.Error!.Message);
        Assert.Equal("First report.", order.LockoutCallAttempts);
        _adminNotifier.Verify(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Cleaner_Not_On_The_Job_Reads_It_As_Not_Found()
    {
        var order = ArrangeOrder(assignedEmployeeId: "emp-someone-else");

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Null(order.LockoutReportedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    public async Task A_Finished_Order_Takes_No_Report(OrderStatus status)
    {
        ArrangeOrder(status: status);

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.LockoutOrderClosed, result.Error!.Message);
    }

    [Theory]
    [InlineData("", BusinessErrorMessage.Required)]
    [InlineData(null, BusinessErrorMessage.MaxLength)]
    public async Task The_Note_Of_Call_Attempts_Is_Required_And_Bounded(string? callAttempts, string expected)
    {
        _orders.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new ReportOrderLockout.Validator(_orders.Object).ValidateAsync(
            new ReportOrderLockout.Command(OrderId, callAttempts ?? new string('x', 1001)));

        Assert.Equal(expected, Assert.Single(result.Errors).ErrorMessage);
    }
}
