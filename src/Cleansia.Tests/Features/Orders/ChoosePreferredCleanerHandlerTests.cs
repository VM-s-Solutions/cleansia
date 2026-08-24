using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0045 D5 — the customer's second and final choice, and where the ladder stops.
///
/// <para>The loop terminates on a COUNT, not on the window formula: the formula recomputes off the
/// current lead and decays by ~10% a round, so from a seven-day booking it would take about thirty
/// reservations to reach the eight-hour floor. Every structural refusal answers with the same key,
/// because a per-reason code would let the customer tell "someone else is still considering it" from
/// "your favourite passed".</para>
/// </summary>
public class ChoosePreferredCleanerHandlerTests
{
    private const string OrderId = "order-choose-1";
    private const string CustomerUserId = "user-choose-customer";
    private const string FirstChoiceId = "employee-first-choice";
    private const string SecondChoiceId = "employee-second-choice";
    private const string ThirdChoiceId = "employee-third-choice";
    private const string RivalId = "employee-rival";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPreferredCleanerHoldResolver> _resolver = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();

    public ChoosePreferredCleanerHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        _userMembershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                CustomerUserId, "plan-plus", "sub_choose", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));
        GrantOnResolve();
    }

    [Fact]
    public async Task A_Re_Offer_After_The_Lapse_Reserves_The_Order_For_The_New_Cleaner()
    {
        var order = Arrange(holdLapsedMinutesAgo: 5);

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(SecondChoiceId, order.PreferredEmployeeId);
        Assert.True(order.PreferredHoldUntilUtc > DateTime.UtcNow);
        Assert.Equal(2, order.PreferredOfferRound);
        Assert.Equal(2, result.Value!.Round);
    }

    /// <summary>
    /// The re-offer earns the new cleaner the same targeted push the booking did — otherwise the order
    /// is reserved for someone who was never told.
    /// </summary>
    [Fact]
    public async Task The_Newly_Chosen_Cleaner_Is_Told()
    {
        Arrange(holdLapsedMinutesAgo: 5);

        await CreateHandler().Handle(Command(SecondChoiceId), default);

        _notificationProducer.Verify(
            p => p.NotifyAsync(
                $"user-{SecondChoiceId}",
                NotificationEventCatalog.PreferredOffer,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.Is<string>(subject => subject != null
                    && subject.StartsWith(OrderId + ":", StringComparison.Ordinal)
                    && subject.Length > OrderId.Length + 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The receipt belongs to the RESERVATION, not the order. Without clearing it the lapse sweep's
    /// idempotency term would suppress round two's prompt entirely, and AC4's "never zero" would be
    /// false for every customer who used their re-offer.
    /// </summary>
    [Fact]
    public async Task A_Re_Offer_Re_Arms_The_Lapse_Prompt()
    {
        var order = Arrange(holdLapsedMinutesAgo: 5);
        order.MarkPreferredOfferLapseNotified(DateTime.UtcNow.AddMinutes(-4));

        await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.Null(order.PreferredOfferLapseNotifiedAt);
    }

    /// <summary>
    /// The termination, and the terminal state: after the second round lapses the customer is refused,
    /// and the order is already where "a random cleaner" means — the open board, which the clock put it
    /// on with no code at all.
    /// </summary>
    [Fact]
    public async Task The_Ladder_Stops_At_Two_Rounds_And_The_Order_Reaches_The_Open_Board()
    {
        var order = Arrange(holdLapsedMinutesAgo: 5);

        Assert.True((await CreateHandler().Handle(Command(SecondChoiceId), default)).IsSuccess);

        order.EndPreferredHold(DateTime.UtcNow.AddMinutes(-1));
        var third = await CreateHandler().Handle(Command(ThirdChoiceId), default);

        Assert.False(third.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, third.Error!.Message);
        Assert.Equal(BookingPolicy.MaxPreferredOfferRounds, order.PreferredOfferRound);
        Assert.True(OrderVisibility.NotHeldFrom(order, RivalId, DateTime.UtcNow));
    }

    /// <summary>
    /// A live beneficiary was told the job was theirs to confirm, and ADR-0036 D4 forbids ever telling
    /// them they were passed over — so the customer's second choice becomes available when the
    /// reservation ENDS, which is the instant the closure message reaches them.
    /// </summary>
    [Fact]
    public async Task A_Live_Beneficiary_Cannot_Be_Evicted()
    {
        var order = Arrange(holdLapsedMinutesAgo: null);

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, result.Error!.Message);
        Assert.Equal(FirstChoiceId, order.PreferredEmployeeId);
    }

    /// <summary>The person who just lapsed. Refused mechanically, so the customer is prevented from the
    /// mistake rather than told about a person.</summary>
    [Fact]
    public async Task Re_Offering_The_Same_Cleaner_Is_Refused()
    {
        Arrange(holdLapsedMinutesAgo: 5);

        var result = await CreateHandler().Handle(Command(FirstChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, result.Error!.Message);
    }

    [Fact]
    public async Task An_Order_That_Already_Has_A_Cleaner_Is_Refused()
    {
        var order = Arrange(holdLapsedMinutesAgo: 5);
        order.SetMaxEmployees(2);
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(RivalId)));

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, result.Error!.Message);
    }

    /// <summary>
    /// D5.1 refusal (4) — REFUSE, not grant-and-burn. A re-offer that cannot withhold a seat is not a
    /// reservation: it would push a named cleaner about an order the whole board already holds, burn the
    /// customer's final round, leave the state at None, and then produce a closure message on the next
    /// sweep tick for a reservation that never existed.
    /// </summary>
    [Fact]
    public async Task A_Lead_Time_Too_Short_To_Withhold_A_Seat_Is_Refused_Rather_Than_Burned()
    {
        var order = Arrange(holdLapsedMinutesAgo: 5, cleaningInHours: 5);

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, result.Error!.Message);
        Assert.Equal(1, order.PreferredOfferRound);
        _resolver.Verify(
            r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// D6.2 — a per-occurrence choice would not persist (the materializer re-grants the template's
    /// ORIGINAL favourite the following week) and the per-order round cap would reset weekly, so on a
    /// schedule's life "exactly one re-offer" would be false without bound.
    /// </summary>
    [Fact]
    public async Task A_Recurring_Occurrence_Is_Refused()
    {
        Arrange(holdLapsedMinutesAgo: 5, recurringTemplateId: "tmpl-weekly");

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, result.Error!.Message);
    }

    /// <summary>
    /// The resolver's own declines — a cleaner who left, muted new jobs, works another country, or is
    /// busy in the slot — land on the same key. The customer learns their choice is unavailable and
    /// nothing about the person.
    /// </summary>
    [Fact]
    public async Task A_Resolver_Decline_Is_Refused_With_The_Same_Key()
    {
        Arrange(holdLapsedMinutesAgo: 5);
        _resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerBusyAtCleaningTime));

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, result.Error!.Message);
    }

    /// <summary>S3 — a resource-by-id command answers a non-owner exactly as it answers a stranger.</summary>
    [Fact]
    public async Task Another_Customers_Order_Is_Not_Found()
    {
        Arrange(holdLapsedMinutesAgo: 5);
        _session.Setup(s => s.GetUserId()).Returns("user-someone-else");

        var result = await CreateHandler().Handle(Command(SecondChoiceId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    /// <summary>
    /// The order's own persisted slot decides the answer, never a client field: the resolver is asked
    /// about the booking as stored.
    /// </summary>
    [Fact]
    public async Task The_Resolver_Is_Asked_About_The_Orders_Own_Persisted_Slot()
    {
        var order = Arrange(holdLapsedMinutesAgo: 5);

        await CreateHandler().Handle(Command(SecondChoiceId), default);

        _resolver.Verify(
            r => r.ResolveAsync(
                CustomerUserId,
                SecondChoiceId,
                "cz",
                order.CleaningDateTime,
                order.EstimatedTime,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ChoosePreferredCleaner.Command Command(string employeeId) => new(OrderId, employeeId);

    private ChoosePreferredCleaner.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _resolver.Object,
            _notificationProducer.Object,
            _userMembershipRepository.Object);

    private void GrantOnResolve()
    {
        _resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? _, string? employeeId, string? _, DateTime _, int _, DateTime nowUtc,
                CancellationToken _) =>
                PreferredCleanerOutcome.Granted(
                    nowUtc.AddHours(2), new PreferredCleanerRecipient($"user-{employeeId}", null)));
    }

    private Order Arrange(
        int? holdLapsedMinutesAgo,
        double cleaningInHours = 48,
        string? recurringTemplateId = null)
    {
        var order = Order.Create(
            customerName: "Choose Customer",
            customerEmail: "choose@cleansia.test",
            customerPhone: "+420777666777",
            customerAddress: Address.Create("Choose St 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            preferredEmployeeId: FirstChoiceId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        var lapsesAt = holdLapsedMinutesAgo is { } ago
            ? DateTime.UtcNow.AddMinutes(-ago)
            : DateTime.UtcNow.AddHours(2);
        order.GrantPreferredHold(
            FirstChoiceId, lapsesAt, lapsesAt.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static Employee NewCleaner(string employeeId)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test", "Test-password-1!", "Rita", "Rival", UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }
}
