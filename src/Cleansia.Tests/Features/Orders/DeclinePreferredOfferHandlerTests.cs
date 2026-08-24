using Cleansia.Core.AppServices.Authentication;
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
/// ADR-0045 D6.4 — a decline ends the reservation IMMEDIATELY and tells the customer at once, rather
/// than leaving them waiting out a deadline that could have ended in seconds. It is one write; nothing
/// about the refusal is recorded, so afterwards the platform cannot tell a decline from a silence.
/// </summary>
public class DeclinePreferredOfferHandlerTests
{
    private const string OrderId = "order-decline-1";
    private const string BeneficiaryId = "employee-beneficiary";
    private const string RivalId = "employee-rival";
    private const string CustomerUserId = "user-customer";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();

    [Fact]
    public async Task A_Decline_Ends_The_Reservation_Now()
    {
        var order = Arrange(caller: BeneficiaryId, holdUntilUtc: DateTime.UtcNow.AddHours(3));

        var result = await CreateHandler().Handle(new DeclinePreferredOffer.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.True(order.PreferredHoldUntilUtc <= DateTime.UtcNow);
    }

    /// <summary>The order is back with the whole board the instant the decline lands — no sweep, no
    /// timer, no status transition in between.</summary>
    [Fact]
    public async Task A_Declined_Order_Is_Immediately_Open_To_Every_Other_Cleaner()
    {
        var order = Arrange(caller: BeneficiaryId, holdUntilUtc: DateTime.UtcNow.AddHours(3));
        Assert.False(OrderVisibility.NotHeldFrom(order, RivalId, DateTime.UtcNow));

        await CreateHandler().Handle(new DeclinePreferredOffer.Command(OrderId), default);

        Assert.True(OrderVisibility.NotHeldFrom(order, RivalId, DateTime.UtcNow));
    }

    [Fact]
    public async Task A_Decline_Tells_The_Customer_At_Once()
    {
        var order = Arrange(caller: BeneficiaryId, holdUntilUtc: DateTime.UtcNow.AddHours(3));

        await CreateHandler().Handle(new DeclinePreferredOffer.Command(OrderId), default);

        _notificationProducer.Verify(
            p => p.NotifyAsync(
                CustomerUserId,
                NotificationEventCatalog.PreferredOfferClosed,
                It.Is<Dictionary<string, string>>(a => a["orderId"] == OrderId),
                It.IsAny<string?>(),
                It.Is<string>(subject => subject != null
                    && subject.StartsWith(OrderId + ":", StringComparison.Ordinal)
                    && subject.Length > OrderId.Length + 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(order.PreferredOfferLapseNotifiedAt);
    }

    /// <summary>
    /// The receipt is what makes the 5-minute sweep skip an order the decline already announced. Two
    /// prompts for one reservation is AC4's "never both" in its other direction.
    /// </summary>
    [Fact]
    public async Task Declining_Twice_Announces_Once()
    {
        Arrange(caller: BeneficiaryId, holdUntilUtc: DateTime.UtcNow.AddHours(3));
        var handler = CreateHandler();

        await handler.Handle(new DeclinePreferredOffer.Command(OrderId), default);
        var second = await handler.Handle(new DeclinePreferredOffer.Command(OrderId), default);

        Assert.True(second.IsSuccess);
        _notificationProducer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A held order has to be indistinguishable from a missing one, or the fact that someone else was
    /// named leaks out of the refusal. Same gate, same key, as the take.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_The_Order_Is_Held_From_Cannot_Tell_It_Apart_From_A_Missing_One()
    {
        Arrange(caller: RivalId, holdUntilUtc: DateTime.UtcNow.AddHours(3));

        var result = await CreateHandler().Handle(new DeclinePreferredOffer.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    /// <summary>
    /// A bystander on an unheld order writes nothing and learns nothing — reporting an error would let
    /// them probe whether an order carries a live reservation.
    /// </summary>
    [Fact]
    public async Task A_Bystander_On_An_Open_Order_Changes_Nothing()
    {
        var order = Arrange(caller: RivalId, holdUntilUtc: null);

        var result = await CreateHandler().Handle(new DeclinePreferredOffer.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.Null(order.PreferredHoldUntilUtc);
        Assert.Null(order.PreferredOfferLapseNotifiedAt);
        _notificationProducer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// D13 / D6 — a decline stores nothing about the refusal and spends no round. The beneficiary stays
    /// on the row on purpose: it is what lets the customer's re-offer refuse the same person without
    /// anybody being told who it was.
    /// </summary>
    [Fact]
    public async Task A_Decline_Keeps_The_Beneficiary_And_Spends_No_Round()
    {
        var order = Arrange(caller: BeneficiaryId, holdUntilUtc: DateTime.UtcNow.AddHours(3));

        await CreateHandler().Handle(new DeclinePreferredOffer.Command(OrderId), default);

        Assert.Equal(BeneficiaryId, order.PreferredEmployeeId);
        Assert.Equal(1, order.PreferredOfferRound);
    }

    private DeclinePreferredOffer.Handler CreateHandler() =>
        new(_orderRepository.Object, _orderAccessService.Object, _notificationProducer.Object);

    private Order Arrange(string caller, DateTime? holdUntilUtc)
    {
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var order = Order.Create(
            customerName: "Decline Customer",
            customerEmail: "decline@cleansia.test",
            customerPhone: "+420777555666",
            customerAddress: Address.Create("Decline St 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            preferredEmployeeId: BeneficiaryId);
        order.Id = OrderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        if (holdUntilUtc is { } until)
        {
            order.GrantPreferredHold(
                BeneficiaryId, until, DateTime.UtcNow, BookingPolicy.MaxPreferredOfferRounds);
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }
}
