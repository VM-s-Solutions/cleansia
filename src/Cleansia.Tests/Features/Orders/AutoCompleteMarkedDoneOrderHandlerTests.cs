using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-26: "If everyone marked that they're done, but didn't click on a shared button then
/// yes. If not then they're still cleaning". A crew job everyone marked done by the cutoff closes through the
/// completion a cleaner's Complete runs, every seat paid; anything else is left exactly as it is.
/// </summary>
public class AutoCompleteMarkedDoneOrderHandlerTests
{
    private const string OrderId = "order-auto-close";
    private const string CleanerA = "emp-auto-close-a";
    private const string CleanerB = "emp-auto-close-b";
    private const string Company = "tenant-auto-close";
    private const string CustomerId = "user-auto-close";

    internal static readonly DateTime Now = DateTime.UtcNow;
    internal static readonly DateTime Cutoff = Now.AddHours(-2);
    private static readonly DateTime StartedAt = Now.AddHours(-3);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderPhotoRepository> _photos = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IReferralService> _referralService = new();

    private AutoCompleteMarkedDoneOrder.Handler Handler() => new(
        _orderRepository.Object,
        _photos.Object,
        _tenantProvider.Object,
        _pending.Object,
        _notificationProducer.Object,
        _liveActivityProducer.Object,
        _loyaltyService.Object,
        _referralService.Object);

    /// <summary>A two-seat card job its crew started three hours ago, under its own company.</summary>
    internal static Order TwoSeatJob(
        DateTime? markA,
        DateTime? markB,
        OrderStatus status = OrderStatus.InProgress,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string orderId = OrderId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: StartedAt,
            paymentType: PaymentType.Card,
            totalPrice: 2400m,
            currencyId: ValidatorTestHelpers.CurrencyId,
            paymentStatus: paymentStatus,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.TenantId = Company;
        order.SetMaxEmployees(2);

        var at = new DateTimeOffset(StartedAt.AddHours(-2), TimeSpan.Zero);
        foreach (var track in new[] { OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress })
        {
            var row = OrderStatusTrack.Create(track, order);
            row.Created("test", track == OrderStatus.InProgress ? new DateTimeOffset(StartedAt, TimeSpan.Zero) : at);
            order.AddOrderStatus(row);
            at = at.AddMinutes(30);
        }

        if (status != OrderStatus.InProgress)
        {
            var row = OrderStatusTrack.Create(status, order);
            row.Created("test", new DateTimeOffset(Now.AddMinutes(-1), TimeSpan.Zero));
            order.AddOrderStatus(row);
        }

        foreach (var (employeeId, mark) in new[] { (CleanerA, markA), (CleanerB, markB) })
        {
            var seat = OrderEmployee.Create(order, ValidatorTestHelpers.BuildEmployee(employeeId, ContractStatus.Approved));
            if (mark is not null)
            {
                seat.MarkCompletionConfirmed(mark.Value);
            }

            order.AddAssignedEmployee(seat);
        }

        return order;
    }

    private void Arrange(Order order, int afterPhotos = 1)
    {
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _photos.Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterPhotos);
    }

    private void AssertNothingWasDone()
    {
        _pending.VerifyNoOtherCalls();
        _notificationProducer.VerifyNoOtherCalls();
        _liveActivityProducer.VerifyNoOtherCalls();
        _loyaltyService.VerifyNoOtherCalls();
        _referralService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task The_Job_Row_Is_Locked_Before_The_Job_Is_Read()
    {
        var order = TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10));
        Arrange(order);
        var calls = new List<string>();
        _orderRepository.Setup(r => r.LockForCrewChangeAsync(OrderId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("lock"))
            .Returns(Task.CompletedTask);
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Callback(() => calls.Add("read"))
            .Returns(new[] { order }.AsQueryable().BuildMock());

        await Handler().Handle(new AutoCompleteMarkedDoneOrder.Command(OrderId, Cutoff), default);

        Assert.Equal(["lock", "read"], calls);
    }

    /// <summary>
    /// The completion a cleaner's Complete runs, under the job's own company: the Completed row, the
    /// customer's push and e-mail, the receipt, points, the referral and every seat's pay. The job is timed
    /// from its start to the last mark, since the wait for somebody to press Complete was not work.
    /// </summary>
    [Fact]
    public async Task A_Crew_That_All_Marked_Done_By_The_Cutoff_Is_Completed_And_Every_Seat_Paid()
    {
        var order = TwoSeatJob(Cutoff.AddMinutes(-40), Cutoff.AddMinutes(-12));
        Arrange(order);

        var result = await Handler().Handle(new AutoCompleteMarkedDoneOrder.Command(OrderId, Cutoff), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value.Completed);
        Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
        Assert.Equal(48, order.ActualCompletionTime);
        Assert.Null(order.CompletionNotes);
        _tenantProvider.Verify(t => t.SetTenantOverride(Company), Times.Once);
        foreach (var cleaner in new[] { CleanerA, CleanerB })
        {
            _pending.Verify(
                p => p.Enqueue(
                    QueueNames.CalculateOrderPay,
                    It.IsAny<QueueEnvelope<CalculateOrderPayMessage>>(),
                    MessageKeys.Pay(OrderId, cleaner)),
                Times.Once);
        }

        _pending.Verify(
            p => p.Enqueue(
                QueueNames.SendEmail,
                It.IsAny<QueueEnvelope<SendOrderStatusEmailMessage>>(),
                MessageKeys.OrderStatusEmail(OrderId, OrderStatus.Completed)),
            Times.Once);
        _notificationProducer.Verify(
            n => n.NotifyAsync(
                CustomerId, NotificationEventCatalog.OrderCompleted, It.IsAny<Dictionary<string, string>>(),
                Company, OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
        _loyaltyService.Verify(l => l.GrantForCompletedOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _referralService.Verify(r => r.ProcessOrderCompletedAsync(OrderId, CustomerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    public static TheoryData<string, Order> JobsThatAreNotClosed => new()
    {
        { "someone has not marked done: they are still cleaning", TwoSeatJob(Cutoff.AddMinutes(-30), null) },
        { "nobody has marked done", TwoSeatJob(null, null) },
        { "the last mark is after the cutoff", TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(5)) },
        { "a cleaner completed it meanwhile", TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), OrderStatus.Completed) },
        { "it was cancelled", TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), OrderStatus.Cancelled) },
        { "the card was never charged", TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), paymentStatus: PaymentStatus.Pending) },
    };

    [Theory]
    [MemberData(nameof(JobsThatAreNotClosed))]
    public async Task A_Job_That_Does_Not_Qualify_Is_Left_As_It_Is(string because, Order order)
    {
        Arrange(order);
        var status = order.CurrentStatus;

        var result = await Handler().Handle(new AutoCompleteMarkedDoneOrder.Command(OrderId, Cutoff), default);

        Assert.True(result.IsSuccess, because);
        Assert.False(result.Value.Completed, because);
        Assert.Equal(status, order.CurrentStatus);
        AssertNothingWasDone();
    }

    /// <summary>The order rules a cleaner's Complete meets: without an after photo it would be refused too.</summary>
    [Fact]
    public async Task A_Job_With_No_After_Photo_Is_Left_As_It_Is()
    {
        var order = TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10));
        Arrange(order, afterPhotos: 0);

        var result = await Handler().Handle(new AutoCompleteMarkedDoneOrder.Command(OrderId, Cutoff), default);

        Assert.False(result.Value.Completed);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        AssertNothingWasDone();
    }
}
