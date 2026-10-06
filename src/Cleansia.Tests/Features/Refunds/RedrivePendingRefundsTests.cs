using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// Owner ruling 2026-09-28: a refund Stripe refused or could not be reached for used to
/// stay pending forever with nobody retrying it. Every hour a cancellation's refund is re-driven on its own
/// key, the customer is told when it goes through, and the company's administrators hear of any refund
/// still not through a day after it was asked for — once.
/// </summary>
public class RedrivePendingRefundsTests
{
    private const string TenantId = "tenant-a";
    private const string UserId = "user-refund-1";

    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly Mock<IUserNotificationRepository> _userNotifications = new();
    private readonly Mock<ITenantProvider> _tenants = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly List<AdminEvent> _raised = [];

    public RedrivePendingRefundsTests()
    {
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private RedrivePendingRefunds.Handler Handler() => new(
        _refunds.Object,
        _refundService.Object,
        _notifications.Object,
        _adminNotifier.Object,
        _userNotifications.Object,
        _tenants.Object,
        _uow.Object,
        NullLogger<RedrivePendingRefunds.Handler>.Instance);

    private static Refund PendingRefund(
        string orderId,
        TimeSpan age,
        RefundStatus status = RefundStatus.Pending,
        RefundSource source = RefundSource.AppRefund,
        string? userId = UserId,
        bool orderCancelled = true,
        RefundReason reason = RefundReason.CustomerCancellation,
        string? refundKey = null)
    {
        var order = Order.Create(
            customerName: "Refund",
            customerEmail: "refund@example.test",
            customerPhone: "+420111333555",
            customerAddress: null!,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        if (orderCancelled)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        }

        var refund = Refund.Create(
            orderId, refundKey ?? $"refund:{orderId}:cancel", 1000m, "CZK", reason, source);
        refund.TenantId = TenantId;
        refund.Created("test", DateTimeOffset.UtcNow - age);
        if (status == RefundStatus.Succeeded)
        {
            refund.MarkSucceeded(null, DateTimeOffset.UtcNow);
        }

        typeof(Refund).GetProperty(nameof(Refund.Order))!.SetValue(refund, order);
        return refund;
    }

    private void Arrange(params Refund[] refunds) =>
        _refunds.Setup(r => r.GetQueryableIgnoringTenant()).Returns(refunds.AsQueryable().BuildMock());

    private void RedriveSucceeds(Refund refund) =>
        _refundService.Setup(s => s.RedriveAsync(refund.Id, "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                refund.Id, refund.RefundKey, refund.Amount, RefundStatus.Succeeded, false)));

    private void RedriveFails(Refund refund) =>
        _refundService.Setup(s => s.RedriveAsync(refund.Id, "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(Refund.Amount), BusinessErrorMessage.RefundFailed)));

    private Task<BusinessResult<RedrivePendingRefunds.Response>> RunAsync() =>
        Handler().Handle(new RedrivePendingRefunds.Command(), CancellationToken.None);

    [Fact]
    public async Task A_Pending_Refund_Is_Redriven_And_The_Customer_Is_Told_It_Went_Through()
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(2));
        Arrange(refund);
        RedriveSucceeds(refund);

        var result = await RunAsync();

        Assert.Equal(1, result.Value!.Redriven);
        _notifications.Verify(n => n.NotifyAsync(
            UserId, NotificationEventCatalog.OrderRefunded,
            It.Is<Dictionary<string, string>>(args => args["orderId"] == "order-1"),
            TenantId, refund.Id, It.IsAny<CancellationToken>()), Times.Once);
        _tenants.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Refund_Younger_Than_Half_An_Hour_Is_Left_To_The_Request_That_Made_It()
    {
        Arrange(PendingRefund("order-1", TimeSpan.FromMinutes(5)));

        var result = await RunAsync();

        Assert.Equal(0, result.Value!.Considered);
        _refundService.Verify(s => s.RedriveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Succeeded_And_Chargeback_Rows_Are_Never_Redriven()
    {
        Arrange(
            PendingRefund("order-1", TimeSpan.FromHours(2), status: RefundStatus.Succeeded),
            PendingRefund("order-2", TimeSpan.FromHours(2), source: RefundSource.Chargeback));

        await RunAsync();

        _refundService.Verify(s => s.RedriveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Refund_Still_Not_Through_After_A_Day_Alerts_The_Administrators()
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(25));
        Arrange(refund);
        RedriveFails(refund);

        var result = await RunAsync();

        Assert.Equal(1, result.Value!.Alerted);
        var alert = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.RefundStuck, alert.Key);
        Assert.Equal(TenantId, alert.TenantId);
        Assert.Equal(refund.Id, alert.Subject);
        Assert.Equal("order-1", alert.Args["orderId"]);
        Assert.Equal("1000 CZK", alert.Args["amount"]);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Inside_The_Day_A_Failing_Refund_Is_Retried_Quietly()
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(3));
        Arrange(refund);
        RedriveFails(refund);

        await RunAsync();

        Assert.Empty(_raised);
    }

    [Theory]
    [InlineData(RefundReason.CustomerCancellation, "refund:order-1:cancel", AdminNotificationEventCatalog.RefundStuck)]
    [InlineData(RefundReason.DisputeResolution, "refund:order-1:dispute:dispute-1", AdminNotificationEventCatalog.RefundNeedsRetry)]
    public async Task The_Administrators_Are_Told_Once_Not_Every_Hour(RefundReason reason, string refundKey, string raisedAs)
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(30), reason: reason, refundKey: refundKey);
        Arrange(refund);
        RedriveFails(refund);
        _userNotifications
            .Setup(r => r.AnyForEventAsync(TenantId, raisedAs, "orderId", "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await RunAsync();

        Assert.Empty(_raised);
    }

    /// <summary>A Stripe outage on one refund leaves it pending and does not stop the others.</summary>
    [Fact]
    public async Task A_Stripe_Outage_On_One_Refund_Does_Not_Stop_The_Next()
    {
        var first = PendingRefund("order-1", TimeSpan.FromHours(3));
        var second = PendingRefund("order-2", TimeSpan.FromHours(2));
        Arrange(first, second);
        _refundService.Setup(s => s.RedriveAsync(first.Id, "system", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));
        RedriveSucceeds(second);

        var result = await RunAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Considered);
        Assert.Equal(1, result.Value.Redriven);
    }

    /// <summary>
    /// A row that fails for any other reason — a commit refused for a frozen company's books, say — is
    /// rolled back and logged, and the rows behind it still run. Oldest first, it would otherwise block
    /// every refund behind it on every run.
    /// </summary>
    [Fact]
    public async Task A_Row_That_Cannot_Be_Settled_Does_Not_Starve_The_Rows_Behind_It()
    {
        var first = PendingRefund("order-1", TimeSpan.FromHours(3));
        var second = PendingRefund("order-2", TimeSpan.FromHours(2));
        Arrange(first, second);
        _refundService.Setup(s => s.RedriveAsync(first.Id, "system", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the company's books are frozen"));
        RedriveSucceeds(second);

        var result = await RunAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Redriven);
        _uow.Verify(u => u.Rollback(), Times.Once);
        _refundService.Verify(s => s.RedriveAsync(second.Id, "system", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A cancellation's refund, from the customer or the platform, and a no-show's are each the order's
    /// one refund for that purpose on an order already cancelled; finishing them finishes the job.
    /// </summary>
    [Theory]
    [InlineData(RefundReason.CustomerCancellation, "cancel")]
    [InlineData(RefundReason.ServiceNotRendered, "admin")]
    public async Task A_Cancelled_Orders_Own_Refund_Is_Redriven(RefundReason reason, string purpose)
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(2), reason: reason, refundKey: $"refund:order-1:{purpose}");
        Arrange(refund);
        RedriveSucceeds(refund);

        var result = await RunAsync();

        Assert.Equal(1, result.Value!.Redriven);
    }

    /// <summary>
    /// A guest's refund is claimed before the cancel. When Stripe cannot be reached the request fails and
    /// the booking stays live, so re-driving that claim would refund a clean that still happens. But a
    /// timeout can come after Stripe took the refund, and every later refund on the order counts the claim
    /// as money already on its way back, so a day later the administrators are told, on an event of its own:
    /// neither the dispute nor the order's Full refund retries this claim.
    /// </summary>
    [Fact]
    public async Task A_Refund_Whose_Cancel_Never_Committed_Is_Raised_For_A_Retry_But_Never_Redriven()
    {
        Arrange(PendingRefund("order-1", TimeSpan.FromHours(25), userId: null, orderCancelled: false));

        var result = await RunAsync();

        Assert.Equal(0, result.Value!.Redriven);
        Assert.Equal(1, result.Value.Alerted);
        _refundService.Verify(s => s.RedriveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var alert = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.RefundWithoutCancel, alert.Key);
        Assert.Equal("order-1", alert.Args["orderId"]);
        Assert.Equal("1000 CZK", alert.Args["amount"]);
    }

    [Fact]
    public async Task A_Refund_Whose_Cancel_Never_Committed_Raises_Nothing_Inside_The_Day()
    {
        Arrange(PendingRefund("order-1", TimeSpan.FromHours(2), userId: null, orderCancelled: false));

        var result = await RunAsync();

        Assert.Equal(0, result.Value!.Alerted);
        Assert.Empty(_raised);
        _refundService.Verify(s => s.RedriveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Refund_Whose_Cancel_Never_Committed_Is_Raised_Once_Not_Every_Hour()
    {
        Arrange(PendingRefund("order-1", TimeSpan.FromHours(30), userId: null, orderCancelled: false));
        _userNotifications
            .Setup(r => r.AnyForEventAsync(TenantId, AdminNotificationEventCatalog.RefundWithoutCancel, "orderId", "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await RunAsync();

        Assert.Equal(0, result.Value!.Alerted);
        Assert.Empty(_raised);
    }

    /// <summary>
    /// A dispute's, an admin's or a partial refund belongs to the action that asked for it, whose retry
    /// runs its own follow-up — the dispute's resolution, the loyalty revoke, its own message. The
    /// administrators are told it is stuck and that nothing retries it for them, on a different event
    /// from the one that says a cancellation's refund is retried every hour.
    /// </summary>
    [Theory]
    [InlineData(RefundReason.DisputeResolution, "refund:order-1:dispute:dispute-1", true)]
    [InlineData(RefundReason.AdminDiscretion, "refund:order-1:admin:full", true)]
    [InlineData(RefundReason.CustomerCancellation, "refund:order-1:cancel:line-1", true)]
    [InlineData(RefundReason.DisputeResolution, "refund:order-1:dispute:dispute-1", false)]
    public async Task Another_Actions_Refund_Is_Raised_For_A_Retry_After_A_Day(
        RefundReason reason, string refundKey, bool orderCancelled)
    {
        Arrange(PendingRefund(
            "order-1", TimeSpan.FromHours(25), reason: reason, refundKey: refundKey, orderCancelled: orderCancelled));

        var result = await RunAsync();

        _refundService.Verify(s => s.RedriveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(1, result.Value!.Alerted);
        var alert = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.RefundNeedsRetry, alert.Key);
        Assert.Equal("order-1", alert.Args["orderId"]);
        Assert.Equal("1000 CZK", alert.Args["amount"]);
    }

    /// <summary>
    /// Once the order has nothing left to give back — another refund covered it — the re-drive closes the
    /// row, and nobody is asked to chase money that is not owed.
    /// </summary>
    [Theory]
    [InlineData(BusinessErrorMessage.RefundNothingRefundable)]
    [InlineData(BusinessErrorMessage.RefundOrderNotRefundable)]
    public async Task A_Refund_With_Nothing_Left_To_Give_Back_Raises_No_Alert(string refusal)
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(25));
        Arrange(refund);
        _refundService.Setup(s => s.RedriveAsync(refund.Id, "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(new Error(nameof(Refund.Amount), refusal)));

        var result = await RunAsync();

        Assert.Equal(0, result.Value!.Alerted);
        Assert.Empty(_raised);
    }

    /// <summary>Settled by somebody else since this run read it: they have already told the customer.</summary>
    [Fact]
    public async Task A_Refund_Settled_Meanwhile_Is_Not_Announced_Again()
    {
        var refund = PendingRefund("order-1", TimeSpan.FromHours(2));
        Arrange(refund);
        _refundService.Setup(s => s.RedriveAsync(refund.Id, "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                refund.Id, refund.RefundKey, refund.Amount, RefundStatus.Succeeded, ResolvedToExisting: true)));

        await RunAsync();

        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
