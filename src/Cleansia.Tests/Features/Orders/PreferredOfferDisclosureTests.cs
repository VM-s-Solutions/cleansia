using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0049 — the customer's preferred-offer block is a SENTENCE, and the server stops sending it once
/// that sentence stops being true of the booking. Two false sentences are closed: a concluded booking
/// still saying <i>"this booking is now open to our whole team"</i>, and a live booking a DIFFERENT
/// cleaner already filled saying the same thing for ever.
///
/// <para>The second is why the seat count is a term and no status list can replace one: that order is
/// <c>Confirmed</c> with an assignee, which is inside every "is this order live" set in the tree. It is
/// also why the term is <c>AvailableSpots &lt;= 0</c> and never <c>AssignedEmployees.Count &gt; 0</c> —
/// most bookings carry more than one seat, and on a 1-of-2 booking the sentence is still TRUE. The rule
/// removes false sentences, not stale ones.</para>
///
/// <para>The load-bearing row is <see cref="Withholding_A_Block_Can_Never_Hide_A_Live_Exit"/>:
/// <c>canChooseAnother</c> rides inside the block, so nulling the block has to be PROVED not to hide an
/// exit the server would have accepted.</para>
/// </summary>
public class PreferredOfferDisclosureTests
{
    private const string OrderId = "order-disclosure-1";
    private const string CustomerUserId = "user-disclosure-customer";
    private const string BeneficiaryId = "employee-disclosure-chosen";
    private const string RivalId = "employee-disclosure-rival";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();

    public PreferredOfferDisclosureTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        _session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Customer.ToString()));
        _orderAccessService.Setup(s => s.IsCustomerCaller()).Returns(true);
        _orderAccessService
            .Setup(s => s.CanBrowseOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderAccessService
            .Setup(s => s.CanAccessOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Employee>().AsQueryable().BuildMock());
        _userMembershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                CustomerUserId, "plan-plus", "sub_disclosure", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));
    }

    /// <summary>
    /// The D3 truth table, written out rather than computed — a computed expectation would be the rule
    /// re-implemented and would agree with any mutation of it. <c>Confirmed</c> stands in for "the
    /// booking has not concluded"; limb (a) does not read anything else about a live status.
    /// </summary>
    [Theory]
    // Limb (a) — the booking concluded. Every state, both seat counts, both terminal statuses.
    [InlineData(PreferredOfferState.None, OrderStatus.Completed, 1, false)]
    [InlineData(PreferredOfferState.None, OrderStatus.Completed, 0, false)]
    [InlineData(PreferredOfferState.None, OrderStatus.Cancelled, 1, false)]
    [InlineData(PreferredOfferState.None, OrderStatus.Cancelled, 0, false)]
    [InlineData(PreferredOfferState.AwaitingConfirmation, OrderStatus.Completed, 1, false)]
    [InlineData(PreferredOfferState.AwaitingConfirmation, OrderStatus.Completed, 0, false)]
    [InlineData(PreferredOfferState.AwaitingConfirmation, OrderStatus.Cancelled, 1, false)]
    [InlineData(PreferredOfferState.AwaitingConfirmation, OrderStatus.Cancelled, 0, false)]
    [InlineData(PreferredOfferState.Accepted, OrderStatus.Completed, 1, false)]
    [InlineData(PreferredOfferState.Accepted, OrderStatus.Completed, 0, false)]
    [InlineData(PreferredOfferState.Accepted, OrderStatus.Cancelled, 1, false)]
    [InlineData(PreferredOfferState.Accepted, OrderStatus.Cancelled, 0, false)]
    [InlineData(PreferredOfferState.Closed, OrderStatus.Completed, 1, false)]
    [InlineData(PreferredOfferState.Closed, OrderStatus.Completed, 0, false)]
    [InlineData(PreferredOfferState.Closed, OrderStatus.Cancelled, 1, false)]
    [InlineData(PreferredOfferState.Closed, OrderStatus.Cancelled, 0, false)]
    // Limb (b) — on a LIVE booking only the closed-and-full corner is withheld.
    [InlineData(PreferredOfferState.None, OrderStatus.Confirmed, 1, true)]
    [InlineData(PreferredOfferState.None, OrderStatus.Confirmed, 0, true)]
    [InlineData(PreferredOfferState.AwaitingConfirmation, OrderStatus.Confirmed, 1, true)]
    [InlineData(PreferredOfferState.AwaitingConfirmation, OrderStatus.Confirmed, 0, true)]
    [InlineData(PreferredOfferState.Accepted, OrderStatus.Confirmed, 1, true)]
    [InlineData(PreferredOfferState.Accepted, OrderStatus.Confirmed, 0, true)]
    [InlineData(PreferredOfferState.Closed, OrderStatus.Confirmed, 1, true)]
    [InlineData(PreferredOfferState.Closed, OrderStatus.Confirmed, 0, false)]
    public void The_Sentence_Is_Disclosed_Only_While_It_Is_True(
        PreferredOfferState state, OrderStatus currentStatus, int availableSpots, bool expected) =>
        Assert.Equal(expected, PreferredOffer.IsDisclosable(state, currentStatus, availableSpots));

    /// <summary>
    /// A 1-of-2 booking whose reservation closed is the row the CH-2 defect would have silenced: the
    /// second seat really is on the open board, so <i>"open to our whole team"</i> is still true. This
    /// reddens the moment limb (b) is written as an assignment count.
    /// </summary>
    [Fact]
    public void A_Partly_Staffed_Booking_Still_Says_The_Board_Is_Open() =>
        Assert.True(PreferredOffer.IsDisclosable(PreferredOfferState.Closed, OrderStatus.Confirmed, 1));

    /// <summary>
    /// ADR-0049 D5, asserted rather than argued. <c>CanChooseAnother</c> rides inside the block, so
    /// withholding the block must never hide an exit the server would accept: <c>¬IsDisclosable ⇒
    /// ¬IsOpen</c> over the constructed state space below.
    ///
    /// <para>Membership is held TRUE throughout because <c>IsOpen</c> is monotone in it — a non-member
    /// makes the implication trivially true and would prove nothing. The lead time is 48 h for the same
    /// reason: inside the dead tail <c>ComputePreferredHold</c> returns zero and every row passes for
    /// free.</para>
    /// </summary>
    [Fact]
    public void Withholding_A_Block_Can_Never_Hide_A_Live_Exit()
    {
        var rows = BuildStateSpace(DateTime.UtcNow);

        var hidden = rows
            .Where(r => !r.Disclosable && r.Open)
            .Select(r => r.Label)
            .ToList();

        Assert.True(
            hidden.Count == 0,
            "Withholding the block would hide a live 'choose another cleaner' exit on: "
            + string.Join(" | ", hidden));

        // Anti-vacuity: an implication over a space that contains no antecedent, no true consequent, or
        // no counterexample to the converse holds for reasons that have nothing to do with the rule.
        Assert.Contains(rows, r => !r.Disclosable);
        Assert.Contains(rows, r => r.Open);
        Assert.Contains(rows, r => r.Disclosable && !r.Open);

        // ...and each limb of the withholding reaches the theorem on its own, so neither half of the
        // D5 proof is carried by the other.
        Assert.Contains(rows, r =>
            r.Status is OrderStatus.Completed or OrderStatus.Cancelled
            && !(r.State == PreferredOfferState.Closed && r.AvailableSpots <= 0));
        Assert.Contains(rows, r =>
            r.Status is not (OrderStatus.Completed or OrderStatus.Cancelled)
            && r.State == PreferredOfferState.Closed && r.AvailableSpots <= 0);
    }

    [Fact]
    public async Task A_Cancelled_Booking_Carries_No_Offer_Block()
    {
        ArrangeHandler(OrderStatus.Cancelled, ReservationShape.LapsedHoldNoCleaner);

        var detail = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Assert.Null(detail.Value!.PreferredOffer);
    }

    [Fact]
    public async Task A_Completed_Booking_Carries_No_Offer_Block()
    {
        ArrangeHandler(OrderStatus.Completed, ReservationShape.AcceptedByBeneficiary);

        var detail = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Assert.Null(detail.Value!.PreferredOffer);
    }

    /// <summary>
    /// Harm (1): a live <c>Confirmed</c> booking that a different cleaner filled. No status test can
    /// reach it — the distinguishing fact is that there is no seat left.
    /// </summary>
    [Fact]
    public async Task A_Live_Booking_Someone_Else_Filled_Carries_No_Offer_Block()
    {
        ArrangeHandler(OrderStatus.Confirmed, ReservationShape.LapsedHoldFullyStaffed);

        var detail = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Assert.Null(detail.Value!.PreferredOffer);
    }

    [Fact]
    public async Task A_Live_Booking_With_A_Seat_Left_Still_Carries_The_Offer_Block()
    {
        ArrangeHandler(OrderStatus.Confirmed, ReservationShape.LapsedHoldOneRival);

        var detail = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Assert.Equal(PreferredOfferState.Closed, detail.Value!.PreferredOffer!.State);
    }

    private sealed record Row(
        string Label,
        OrderStatus Status,
        PreferredOfferState State,
        int AvailableSpots,
        bool Disclosable,
        bool Open);

    private static List<Row> BuildStateSpace(DateTime nowUtc)
    {
        var rows = new List<Row>();

        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            foreach (var paymentType in new[] { PaymentType.Cash, PaymentType.Card })
            {
                foreach (var paymentStatus in new[] { PaymentStatus.Pending, PaymentStatus.Paid })
                {
                    foreach (var recurringTemplateId in new string?[] { null, "tmpl-weekly" })
                    {
                        foreach (var shape in Enum.GetValues<ReservationShape>())
                        {
                            var order = BuildOrder(
                                status, paymentType, paymentStatus, recurringTemplateId, shape, nowUtc);

                            var beneficiaryIsAssigned = !string.IsNullOrEmpty(order.PreferredEmployeeId)
                                && order.AssignedEmployees.Any(ae => ae.EmployeeId == order.PreferredEmployeeId);
                            var state = PreferredOffer.StateOf(
                                order.PreferredEmployeeId, order.PreferredHoldUntilUtc,
                                beneficiaryIsAssigned, nowUtc);

                            rows.Add(new Row(
                                $"{status}/{paymentType}/{paymentStatus}/"
                                + $"{(recurringTemplateId is null ? "one-off" : "recurring")}/{shape}",
                                status,
                                state,
                                order.AvailableSpots,
                                PreferredOffer.IsDisclosable(state, order.CurrentStatus, order.AvailableSpots),
                                PreferredOfferExit.IsOpen(order, callerHasActiveMembership: true, nowUtc)));
                        }
                    }
                }
            }
        }

        return rows;
    }

    private enum ReservationShape
    {
        NoReservationNoCleaner,
        NoReservationOneCleaner,
        NoReservationFullyStaffed,
        LiveHold,
        LapsedHoldNoCleaner,
        LapsedHoldOneRival,
        LapsedHoldFullyStaffed,
        AcceptedByBeneficiary,
        AcceptedPlusRival,
    }

    private static Order BuildOrder(
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId,
        ReservationShape shape,
        DateTime nowUtc)
    {
        var order = Order.Create(
            customerName: "Disclosure Customer",
            customerEmail: "disclosure@cleansia.test",
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Disclosure St 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: nowUtc.AddHours(48),
            paymentType: paymentType,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: paymentStatus,
            userId: CustomerUserId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(2);
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech Koruna", 1m));
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));

        // The hold must be granted before anybody is assigned — the aggregate refuses a reservation on
        // an order that already has a cleaner, which is the real ordering every production path takes.
        switch (shape)
        {
            case ReservationShape.LiveHold:
            case ReservationShape.AcceptedByBeneficiary:
            case ReservationShape.AcceptedPlusRival:
                order.GrantPreferredHold(
                    BeneficiaryId, nowUtc.AddHours(2), nowUtc, BookingPolicy.MaxPreferredOfferRounds);
                break;
            case ReservationShape.LapsedHoldNoCleaner:
            case ReservationShape.LapsedHoldOneRival:
            case ReservationShape.LapsedHoldFullyStaffed:
                var lapsedAt = nowUtc.AddMinutes(-5);
                order.GrantPreferredHold(
                    BeneficiaryId, lapsedAt, lapsedAt.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);
                break;
        }

        switch (shape)
        {
            case ReservationShape.NoReservationOneCleaner:
            case ReservationShape.LapsedHoldOneRival:
                order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(RivalId)));
                break;
            case ReservationShape.NoReservationFullyStaffed:
            case ReservationShape.LapsedHoldFullyStaffed:
                order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(RivalId)));
                order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner($"{RivalId}-2")));
                break;
            case ReservationShape.AcceptedByBeneficiary:
                order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(BeneficiaryId)));
                break;
            case ReservationShape.AcceptedPlusRival:
                order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(BeneficiaryId)));
                order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(RivalId)));
                break;
        }

        return order;
    }

    private void ArrangeHandler(OrderStatus status, ReservationShape shape)
    {
        var order = BuildOrder(
            status, PaymentType.Card, PaymentStatus.Paid, recurringTemplateId: null, shape, DateTime.UtcNow);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    private GetOrderDetails.Handler CreateDetailHandler() =>
        new(
            _orderRepository.Object,
            _orderAccessService.Object,
            _session.Object,
            _payConfigRepository.Object,
            _orderEmployeePayRepository.Object,
            _orderPhotoRepository.Object,
            _employeeRepository.Object,
            _expressWaiverConsumer.Object,
            _userMembershipRepository.Object);

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
