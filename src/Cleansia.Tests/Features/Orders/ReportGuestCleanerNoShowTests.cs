using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: past the booked start, with nobody having started, a guest
/// reports that the cleaner did not arrive. The report tells the order's administrators — once, whether
/// the reminder sweep or the report gets there first — and moves no money.
/// </summary>
public class ReportGuestCleanerNoShowTests
{
    private const string OrderId = "order-guest-no-show";
    private const string TenantId = "tenant-a";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _tokens = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly Mock<IUserNotificationRepository> _userNotifications = new();
    private readonly List<AdminEvent> _raised = [];
    private string _token = null!;

    public ReportGuestCleanerNoShowTests()
    {
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private ReportGuestCleanerNoShow.Handler Handler() => new(
        new GuestOrderAccess(_orders.Object, _tokens.Object),
        _adminNotifier.Object,
        _userNotifications.Object,
        TimeProvider.System);

    private Order ArrangeGuestOrder(
        OrderStatus status = OrderStatus.Confirmed, double startedMinutesAgo = 20, bool staffed = true)
    {
        var order = Order.Create(
            customerName: "Guest",
            customerEmail: "guest@example.test",
            customerPhone: "+420111222333",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-startedMinutesAgo),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
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

        if (staffed)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee("emp-guest", ContractStatus.Approved)));
        }

        var token = GuestOrderAccessToken.Issue(OrderId, DateTimeOffset.UtcNow.AddDays(30));
        _token = token.RawToken!;
        _orders.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { order }.AsQueryable().BuildMock());
        _tokens.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { token }.AsQueryable().BuildMock());
        return order;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<ReportGuestCleanerNoShow.Response>> ReportAsync(
        string? token = null) =>
        Handler().Handle(new ReportGuestCleanerNoShow.Command(token ?? _token), CancellationToken.None);

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.OnTheWay)]
    public async Task A_Report_After_The_Start_Tells_The_Orders_Administrators(OrderStatus status)
    {
        var order = ArrangeGuestOrder(status);

        var result = await ReportAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        var alert = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderCleanerNotStarted, alert.Key);
        Assert.Equal(TenantId, alert.TenantId);
        Assert.Equal(OrderId, alert.Subject);
        Assert.Equal(OrderId, alert.Args["orderId"]);
        Assert.Equal(order.DisplayOrderNumber, alert.Args["orderNumber"]);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
    }

    [Fact]
    public async Task The_Administrators_Are_Told_Once_Whoever_Told_Them_First()
    {
        ArrangeGuestOrder();
        _userNotifications
            .Setup(r => r.AnyForEventAsync(
                TenantId, AdminNotificationEventCatalog.OrderCleanerNotStarted, "orderId", OrderId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await ReportAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(_raised);
    }

    /// <summary>
    /// With nobody on the job there is no cleaner to be missing; the unfilled sweep cancels and refunds it,
    /// so the administrators are not asked to confirm a no-show.
    /// </summary>
    [Fact]
    public async Task A_Booking_Nobody_Was_Assigned_To_Raises_No_Alert()
    {
        ArrangeGuestOrder(staffed: false);

        var result = await ReportAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Cleaner_Who_Has_Started_Is_Not_Reported()
    {
        ArrangeGuestOrder(OrderStatus.InProgress);

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.OrderCleanerAlreadyStarted, result.Error!.Message);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task Before_The_Start_There_Is_Nothing_To_Report()
    {
        ArrangeGuestOrder(startedMinutesAgo: -45);

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.OrderStartTimeNotReached, result.Error!.Message);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Cancelled_Booking_Is_Refused()
    {
        ArrangeGuestOrder(OrderStatus.Cancelled);

        var result = await ReportAsync();

        Assert.Equal(BusinessErrorMessage.OrderAlreadyCancelled, result.Error!.Message);
    }

    [Fact]
    public async Task A_Token_Nobody_Issued_Finds_No_Booking()
    {
        ArrangeGuestOrder();

        var result = await ReportAsync(Cleansia.Core.Domain.Common.SecurityTokens.Generate(
            Cleansia.Core.Domain.Common.SecurityTokens.DurableTokenByteLength));

        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Empty(_raised);
    }
}
