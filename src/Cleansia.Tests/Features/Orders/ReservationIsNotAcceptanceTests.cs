using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
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
/// ADR-0045 D2, third leg. <c>CancellationAssessor</c> prices the fee off ONE boolean —
/// <c>order.AssignedEmployees.Count &gt; 0</c> — and that same boolean decides whether cancelling
/// forfeits a live express waiver. Representing the reservation as an <c>OrderEmployee</c> row would
/// therefore make <b>every favourite-cleaner booking fee-bearing and waiver-burning from the instant it
/// was created</b>, before any human agreed to anything: a money defect manufactured by a display
/// feature.
///
/// <para>Read through the customer's own preview, which is the surface that quotes the number, so the
/// assertion is on what the customer would actually be shown rather than on an internal function's
/// argument.</para>
/// </summary>
public class ReservationIsNotAcceptanceTests
{
    private const string OrderId = "order-reserved-fee";
    private const string UserId = "user-reserved";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    public ReservationIsNotAcceptanceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    [Fact]
    public async Task Cancelling_During_A_Live_Reservation_Is_Free()
    {
        ArrangeReservedOrder(cleaningInHours: 24);

        var preview = await CreateHandler().Handle(new GetCancellationFeePreview.Query(OrderId), default);

        Assert.True(preview.IsSuccess);
        Assert.Equal(CancellationFeeTier.FreeNotAccepted, preview.Value!.Tier);
        Assert.Equal(0m, preview.Value.FeeRate);
        Assert.Equal(preview.Value.TotalPrice, preview.Value.RefundAmount);
    }

    /// <summary>
    /// The case the fee tier would otherwise bite hardest: two hours out, where an accepted job costs
    /// 50%. The reservation changes nothing, because it is not an acceptance.
    /// </summary>
    [Fact]
    public async Task A_Reservation_Inside_The_Last_Minute_Window_Is_Still_Free()
    {
        ArrangeReservedOrder(cleaningInHours: 2);

        var preview = await CreateHandler().Handle(new GetCancellationFeePreview.Query(OrderId), default);

        Assert.Equal(CancellationFeeTier.FreeNotAccepted, preview.Value!.Tier);
        Assert.Equal(0m, preview.Value.FeeRate);
    }

    /// <summary>
    /// The waiver half of the same boolean: the release rule is asked "has a cleaner been pulled onto
    /// this job", and during a reservation the honest answer is no — so the slot goes back to the pool
    /// instead of being burned on a booking nobody ever took.
    /// </summary>
    [Fact]
    public async Task A_Reservation_Does_Not_Burn_The_Customers_Express_Waiver()
    {
        ArrangeReservedOrder(cleaningInHours: 24);

        await CreateHandler().Handle(new GetCancellationFeePreview.Query(OrderId), default);

        _expressWaiverConsumer.Verify(
            c => c.WouldForfeitOnCustomerCancelAsync(OrderId, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The control. The same order, the same instant, once a cleaner has actually committed — so the
    /// three assertions above cannot be passing because the fixture is unchargeable for some other
    /// reason.
    /// </summary>
    [Fact]
    public async Task An_Assignment_On_The_Same_Order_Makes_It_Fee_Bearing_And_Waiver_Burning()
    {
        var order = ArrangeReservedOrder(cleaningInHours: 2);
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee()));

        var preview = await CreateHandler().Handle(new GetCancellationFeePreview.Query(OrderId), default);

        Assert.Equal(CancellationFeeTier.LastMinute, preview.Value!.Tier);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, preview.Value.FeeRate);
        _expressWaiverConsumer.Verify(
            c => c.WouldForfeitOnCustomerCancelAsync(OrderId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private GetCancellationFeePreview.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            new CancellationPolicyResolver(_membershipRepository.Object),
            _expressWaiverConsumer.Object);

    private Order ArrangeReservedOrder(double cleaningInHours)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var cleaningDateTime = DateTime.UtcNow.AddHours(cleaningInHours);

        var order = Order.Create(
            customerName: "Reserved Customer",
            customerEmail: "reserved@cleansia.test",
            customerPhone: "+420777333444",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 2000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId,
            preferredEmployeeId: "employee-favourite");
        order.Id = OrderId;
        order.SetMaxEmployees(2);
        order.SetCurrency(currency);

        // Booked well outside the oops window, so a free verdict is the assignment term rather than the
        // accidental-tap grace period.
        order.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        order.GrantPreferredHold(
            "employee-favourite",
            cleaningDateTime.AddMinutes(-30),
            DateTime.UtcNow.AddDays(-2),
            BookingPolicy.MaxPreferredOfferRounds);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static Employee NewEmployee()
    {
        var user = User.CreateWithPassword(
            "cleaner-reserved@cleansia.test", "Test-password-1!", "Ida", "Assigned", UserProfile.Employee);
        var employee = Employee.CreateWithUser(user);
        employee.Id = "employee-assigned";
        return employee;
    }
}
