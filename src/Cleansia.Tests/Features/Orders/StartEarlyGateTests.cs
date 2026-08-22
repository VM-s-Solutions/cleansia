using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The clock gate on the two transitions a cleaner drives.
///
/// <para><b>Neither command read <c>CleaningDateTime</c> at all</b> until 2026-08-22 — a cleaner could
/// mark themselves on the way, start the job and complete it days before the booking. That is not a
/// cosmetic defect: an early start puts the order past <c>Confirmed</c>, which is the status
/// <c>SendPreCleaningReminders</c> filters on, so it silently cancels the customer's "starting soon"
/// notice; and an early complete triggers pay calculation for work that has not happened.</para>
///
/// <para>Both transitions share <see cref="BookingPolicy.IsTooEarlyToStart"/> so they cannot answer
/// differently about the same order.</para>
/// </summary>
public class StartEarlyGateTests
{
    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();

    private StartOrder.Validator StartValidator() =>
        new(_orderRepository.Object, _employeeRepository.Object, _accessService.Object);

    private NotifyOnTheWay.Validator OnTheWayValidator() =>
        new(_orderRepository.Object, _accessService.Object);

    private void Arrange(DateTime cleaningDateTime, OrderStatus status = OrderStatus.Confirmed)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, status, EmployeeId, cleaningDateTime: cleaningDateTime);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);
    }

    // A day out, an hour and a bit out, and a minute outside the window — the last is the boundary case
    // that a naive `> hours` comparison would let through.
    public static TheoryData<int> TooEarlyMinutes =>
    [
        24 * 60,
        BookingPolicy.StartGraceWindowMinutes + 90,
        BookingPolicy.StartGraceWindowMinutes + 1,
    ];

    [Theory]
    [MemberData(nameof(TooEarlyMinutes))]
    public async Task StartOrder_Refuses_A_Job_That_Is_Still_Too_Far_Away(int minutesAhead)
    {
        Arrange(DateTime.UtcNow.AddMinutes(minutesAhead));

        var result = await StartValidator().ValidateAsync(new StartOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderTooEarlyToStart);
    }

    [Theory]
    [MemberData(nameof(TooEarlyMinutes))]
    public async Task NotifyOnTheWay_Refuses_A_Job_That_Is_Still_Too_Far_Away(int minutesAhead)
    {
        Arrange(DateTime.UtcNow.AddMinutes(minutesAhead));

        var result = await OnTheWayValidator().ValidateAsync(new NotifyOnTheWay.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderTooEarlyToStart);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(-24 * 60)]
    public async Task A_Job_Inside_The_Window_Or_Already_Underway_Is_Startable(int minutesAhead)
    {
        Arrange(DateTime.UtcNow.AddMinutes(minutesAhead));

        var start = await StartValidator().ValidateAsync(new StartOrder.Command(OrderId));
        var onTheWay = await OnTheWayValidator().ValidateAsync(new NotifyOnTheWay.Command(OrderId));

        Assert.True(start.IsValid);
        Assert.True(onTheWay.IsValid);
    }

    /// <summary>
    /// There is deliberately no upper bound. A cleaner running two hours late is still allowed to start
    /// — refusing them would strand a job that is genuinely happening, and the customer is already
    /// standing there.
    /// </summary>
    [Fact]
    public void A_Late_Cleaner_Is_Never_Too_Early()
    {
        var now = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(BookingPolicy.IsTooEarlyToStart(now.AddHours(-6), now));
    }

    /// <summary>
    /// The window is the customer's own notice period: <c>SendPreCleaningReminders</c> promises "about
    /// an hour", and the cleaner's earliest permitted start is the same instant, so a cleaner cannot be
    /// at the door before the customer has been told to expect them.
    /// </summary>
    [Fact]
    public void The_Grace_Window_Matches_The_Customers_Notice()
    {
        Assert.Equal(60, BookingPolicy.StartGraceWindowMinutes);
    }

    /// <summary>
    /// Placement, not just presence: a caller who is not on the job must be told exactly that and learn
    /// nothing about when the booking is — the refusal would otherwise leak another customer's schedule
    /// to anyone who can name an order id.
    /// </summary>
    [Fact]
    public async Task An_Unassigned_Caller_Learns_Nothing_About_The_Schedule()
    {
        Arrange(DateTime.UtcNow.AddDays(1));
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("someone-else");
        _employeeRepository.Setup(r => r.GetByIdAsync("someone-else", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidatorTestHelpers.BuildEmployee("someone-else", ContractStatus.Approved));

        var result = await StartValidator().ValidateAsync(new StartOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotAssignedToOrder);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderTooEarlyToStart);
    }
}
