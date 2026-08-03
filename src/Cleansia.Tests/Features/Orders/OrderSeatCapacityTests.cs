using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The seat rule, pinned at both moments it is evaluated: the computation that sets the cap, and the
/// take gate that spends it. An order carries seats for exactly the crew the work needs — a seat
/// filled beyond <c>RequiredEmployees</c> is a second full wage against the same customer price,
/// because <c>CalculateOrderPay</c> writes one pay row per assigned employee and
/// <c>CalculateAggregatedPay</c> has no crew-size term.
/// </summary>
public class OrderSeatCapacityTests
{
    private const string OrderId = "order-seat-1";
    private const string CallerEmployeeId = "emp-caller";

    [Theory]
    [InlineData(0, 1)]    // no estimate yet — one seat, same as the shortest real job
    [InlineData(1, 1)]
    [InlineData(120, 1)]  // the modal booking: two hours, one cleaner
    [InlineData(121, 2)]
    [InlineData(240, 2)]
    [InlineData(600, 5)]
    public void Seat_Cap_Equals_The_Work_Derived_Crew(int estimatedMinutes, int expectedCrew)
    {
        var order = NewOrder(estimatedMinutes);

        Assert.Equal(expectedCrew, order.RequiredEmployees);
        Assert.Equal(expectedCrew, order.MaxEmployees);
        Assert.Equal(expectedCrew, order.AvailableSpots);
    }

    [Fact]
    public void A_Two_Hour_Job_Admits_One_Cleaner_And_Refuses_The_Second()
    {
        var order = NewOrder(estimatedMinutes: 120);

        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-1")));

        Assert.False(order.HasAvailableSpots);
        Assert.Equal(0, order.AvailableSpots);
        Assert.Throws<InvalidOperationException>(
            () => order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-2"))));
    }

    [Fact]
    public void A_Job_Needing_Two_Admits_Exactly_Two()
    {
        var order = NewOrder(estimatedMinutes: 240);

        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-1")));
        Assert.True(order.HasAvailableSpots);

        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-2")));

        Assert.False(order.HasAvailableSpots);
        Assert.Throws<InvalidOperationException>(
            () => order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-3"))));
    }

    [Fact]
    public async Task Take_Is_Refused_When_A_Two_Hour_Job_Already_Has_Its_One_Cleaner()
    {
        var order = NewOrder(estimatedMinutes: 120);
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-other")));

        var result = await ValidateTake(order);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NoAvailableSpots);
    }

    [Fact]
    public async Task Take_Is_Allowed_On_The_Second_Seat_Of_A_Job_That_Needs_Two()
    {
        var order = NewOrder(estimatedMinutes: 240);
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee("emp-other")));

        var result = await ValidateTake(order);

        Assert.True(result.IsValid);
    }

    private static Order NewOrder(int estimatedMinutes)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);

        order.Id = OrderId;
        order.UpdateEstimatedTime(estimatedMinutes);
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);

        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", DateTimeOffset.UtcNow.AddMinutes(-10));
        order.AddOrderStatus(initial);

        return order;
    }

    private static Employee NewEmployee(string employeeId)
    {
        var user = User.CreateWithPassword($"{employeeId}@example.com", "x", "Emp", "Loyee");
        user.Id = $"{employeeId}-user";

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }

    private static async Task<FluentValidation.Results.ValidationResult> ValidateTake(Order order)
    {
        var caller = ValidatorTestHelpers.BuildEmployee(CallerEmployeeId, ContractStatus.Approved);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        orderRepository
            .Setup(r => r.GetEmployeeOrderCountThisWeekAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                CallerEmployeeId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(CallerEmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { caller }.AsQueryable().BuildMock());

        var accessService = new Mock<IOrderAccessService>();
        accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CallerEmployeeId);

        var validator = new TakeOrder.Validator(
            orderRepository.Object,
            employeeRepository.Object,
            accessService.Object);

        return await validator.ValidateAsync(new TakeOrder.Command(OrderId));
    }
}
