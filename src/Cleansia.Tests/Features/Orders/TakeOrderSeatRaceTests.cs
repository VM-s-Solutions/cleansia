using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The take race. <c>TakeOrder.Validator</c> checks <c>HasAvailableSpots</c> and the handler then calls
/// <c>Order.AddAssignedEmployee</c>, which <b>throws</b> <see cref="InvalidOperationException"/> on a full
/// order — two unlocked reads with a window between them, so the loser of two cleaners tapping the same
/// job got a 500 instead of "this job has been taken". The window stopped being theoretical when
/// <c>BookingPolicy.SpareSeatsPerOrder</c> went to 0: a single-cleaner job now has exactly one seat and
/// no spare, so every second tap lands on a full order.
///
/// <para>The refusal reuses <see cref="BusinessErrorMessage.NoAvailableSpots"/> — the key the validator
/// already emits for the same fact — so the two paths cannot disagree, and it stays ONE error, which is
/// what <c>TakeOrderOfferabilityGateTests</c>' TC-TAKE-ONE-ERROR contract requires of every refusal.</para>
/// </summary>
public class TakeOrderSeatRaceTests
{
    private const string OrderId = "order-race-1";
    private const string EmployeeId = "emp-race-1";
    private const string OtherEmployeeId = "emp-race-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IEmailService> _emailService = new();

    [Fact]
    public async Task A_Seat_Taken_Between_Validation_And_The_Handler_Is_A_Business_Refusal_Not_A_Throw()
    {
        var order = Arrange(ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Confirmed, OtherEmployeeId, maxEmployees: 1));

        var result = await CreateHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NoAvailableSpots, result.Error!.Message);
        Assert.Equal(nameof(TakeOrder.Command.OrderId), result.Error.Code);
        Assert.Single(order.AssignedEmployees);
    }

    [Fact]
    public async Task A_Refused_Take_Confirms_Nothing_And_Tells_Nobody()
    {
        var order = Arrange(ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.New, OtherEmployeeId, maxEmployees: 1));

        await CreateHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.NotEqual(OrderStatus.Confirmed, order.GetCurrentOrderStatus());
        _notificationProducer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Free_Seat_Still_Assigns_The_Cleaner()
    {
        var order = Arrange(ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.New, OtherEmployeeId, maxEmployees: 2));

        var result = await CreateHandler().Handle(new TakeOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeId, result.Value!.EmployeeId);
        Assert.Contains(order.AssignedEmployees, oe => oe.EmployeeId == EmployeeId);
    }

    private Order Arrange(Order order)
    {
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);

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
