using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0045 D1.1 — taking a conflicting job <b>is</b> a decline.
///
/// <para>Nothing re-checks the beneficiary's slot between the grant and the confirmation, deliberately:
/// adding a creation-time or read-time re-check would be a second occupancy predicate. The only way
/// they can become busy is by committing to something else, so that commitment is the moment the
/// reservation becomes unhonourable. Left alone, the customer would go on being told that someone is
/// considering their booking who provably cannot confirm it — for up to twelve hours, on a DISCLOSED
/// reservation.</para>
///
/// <para>Confirming is <c>TakeOrder</c> unchanged, so the order the cleaner just took must earn
/// <c>order.cleaner_assigned</c> and never also the closure message: AC4's "never both".</para>
/// </summary>
public class TakeOrderImplicitDeclineTests
{
    private const string TakenOrderId = "order-taken";
    private const string StrandedOrderId = "order-stranded";
    private const string EmployeeId = "employee-double-booked";
    private const string StrandedCustomerId = "user-stranded-customer";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IEmailService> _emailService = new();

    [Fact]
    public async Task Committing_To_A_Clashing_Job_Ends_The_Reservation_It_Can_No_Longer_Honour()
    {
        var stranded = ReservedOrder(StrandedOrderId, StrandedCustomerId);
        Arrange(stranded);

        await CreateHandler().Handle(new TakeOrder.Command(TakenOrderId), CancellationToken.None);

        Assert.True(stranded.PreferredHoldUntilUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task The_Stranded_Customer_Is_Told_At_Once()
    {
        var stranded = ReservedOrder(StrandedOrderId, StrandedCustomerId);
        Arrange(stranded);

        await CreateHandler().Handle(new TakeOrder.Command(TakenOrderId), CancellationToken.None);

        _notificationProducer.Verify(
            p => p.NotifyAsync(
                StrandedCustomerId,
                NotificationEventCatalog.PreferredOfferClosed,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.Is<string>(subject => subject != null
                    && subject.StartsWith(StrandedOrderId + ":", StringComparison.Ordinal)
                    && subject.Length > StrandedOrderId.Length + 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(stranded.PreferredOfferLapseNotifiedAt);
    }

    /// <summary>
    /// The order the cleaner CONFIRMED is excluded. It carries a live reservation naming the same
    /// cleaner and overlaps its own window, so a query result used without excluding it would announce
    /// a closure for the very reservation that was just honoured.
    /// </summary>
    [Fact]
    public async Task The_Order_They_Just_Confirmed_Is_Never_Announced_As_Closed()
    {
        var taken = ReservedOrder(TakenOrderId, "user-taken-customer");
        Arrange(taken);

        await CreateHandler().Handle(new TakeOrder.Command(TakenOrderId), CancellationToken.None);

        _notificationProducer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(),
                NotificationEventCatalog.PreferredOfferClosed,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.NotNull(taken.PreferredHoldUntilUtc);
        Assert.Null(taken.PreferredOfferLapseNotifiedAt);
    }

    /// <summary>
    /// The query is asked about the window the cleaner just committed to — the same interval the
    /// conflict gate and ADR-0039's picker read, through the same shared window filter.
    /// </summary>
    [Fact]
    public async Task The_Search_Window_Is_The_Job_They_Just_Committed_To()
    {
        var order = Arrange(ReservedOrder(StrandedOrderId, StrandedCustomerId));

        await CreateHandler().Handle(new TakeOrder.Command(TakenOrderId), CancellationToken.None);

        _orderRepository.Verify(
            r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                EmployeeId,
                order.CleaningDateTime,
                order.CleaningDateTime.AddMinutes(order.EstimatedTime),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private Order Arrange(params Order[] liveReservations)
    {
        var taken = ValidatorTestHelpers.BuildEmptyOrder(TakenOrderId, OrderStatus.Confirmed);
        taken.UpdateEstimatedTime(120);

        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved);

        _orderRepository.Setup(r => r.GetQueryable())
            .Returns(new[] { taken }.Concat(liveReservations.Where(o => o.Id != TakenOrderId))
                .AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveReservations);
        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);

        return taken;
    }

    private static Order ReservedOrder(string orderId, string customerUserId)
    {
        var order = Order.Create(
            customerName: "Stranded Customer",
            customerEmail: "stranded@cleansia.test",
            customerPhone: "+420777888999",
            customerAddress: Address.Create("Stranded St 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: customerUserId,
            preferredEmployeeId: EmployeeId);
        order.Id = orderId;
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.GrantPreferredHold(
            EmployeeId, DateTime.UtcNow.AddHours(3), DateTime.UtcNow, BookingPolicy.MaxPreferredOfferRounds);
        return order;
    }

    private TakeOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            _notificationProducer.Object,
            _emailService.Object,
            NullLogger<TakeOrder.Handler>.Instance);
}
