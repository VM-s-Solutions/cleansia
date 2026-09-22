using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D3 (Verification #4) — the start gate: a crew member whose CURRENT seat has no acceptance
/// (an admin placed them, or re-added them after a drop) is refused with the product key the app answers
/// by opening the contract sheet; a non-assignee still answers the assignment refusal and learns nothing;
/// a seat with its row passes the gate.
/// </summary>
public sealed class StartOrderWorkContractGateTests
{
    private const string OrderId = "order-gate-start";
    private const string EmployeeId = "emp-gate-1";
    private const string StrangerId = "emp-gate-stranger";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();

    private StartOrder.Validator CreateValidator() =>
        new(_orderRepository.Object, _employeeRepository.Object, _accessService.Object, _acceptanceRepository.Object);

    private Order Arrange(string caller, bool seatAccepted)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Confirmed, EmployeeId, cleaningDateTime: ValidatorTestHelpers.StartableCleaningTime);
        var seat = order.AssignedEmployees.Single();
        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _employeeRepository.Setup(r => r.GetByIdAsync(caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidatorTestHelpers.BuildEmployee(caller, ContractStatus.Approved));
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _acceptanceRepository.Setup(r => r.AnyForSeatAsync(seat.Id, It.IsAny<CancellationToken>())).ReturnsAsync(seatAccepted);
        _acceptanceRepository.Setup(r => r.AnyForSeatAsync(It.Is<string>(id => id != seat.Id), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return order;
    }

    [Fact]
    public async Task A_Placed_Cleaner_With_No_Row_For_Their_Seat_Is_Refused_As_Acceptance_Required()
    {
        Arrange(EmployeeId, seatAccepted: false);

        var result = await CreateValidator().ValidateAsync(new StartOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.WorkContractAcceptanceRequired);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotAssignedToOrder);
    }

    [Fact]
    public async Task A_Cleaner_Not_On_The_Crew_Still_Answers_The_Assignment_Refusal_And_Never_The_Contract_One()
    {
        Arrange(StrangerId, seatAccepted: false);

        var result = await CreateValidator().ValidateAsync(new StartOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotAssignedToOrder);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.WorkContractAcceptanceRequired);
        _acceptanceRepository.Verify(r => r.AnyForSeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Seat_With_Its_Row_Passes_The_Gate()
    {
        Arrange(EmployeeId, seatAccepted: true);

        var result = await CreateValidator().ValidateAsync(new StartOrder.Command(OrderId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task The_Gate_Is_Keyed_On_The_Callers_Current_Seat()
    {
        var order = Arrange(EmployeeId, seatAccepted: false);

        await CreateValidator().ValidateAsync(new StartOrder.Command(OrderId));

        _acceptanceRepository.Verify(r => r.AnyForSeatAsync(order.AssignedEmployees.Single().Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// ADR-0068 D3 (Verification #4) — the complete gate, for the seat the start gate never sees: on a
/// two-seat crew the second cleaner never starts (the job is already in progress) but may complete, so
/// the act that ends the work carries the same rule after the assignment rule.
/// </summary>
public sealed class CompleteOrderWorkContractGateTests
{
    private const string OrderId = "order-gate-complete";
    private const string CleanerA = "emp-gate-a";
    private const string CleanerB = "emp-gate-b";
    private const string StrangerId = "emp-gate-stranger";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();

    private CompleteOrder.Validator CreateValidator() =>
        new(_orderRepository.Object, _employeeRepository.Object, _orderPhotoRepository.Object, _accessService.Object, _acceptanceRepository.Object);

    /// <summary>A two-seat in-progress job: A took it (a row), B was placed by an admin (no row).</summary>
    private Order ArrangeTwoSeatCrew(string caller)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, CleanerA, PaymentType.Cash, PaymentStatus.Paid, maxEmployees: 2);
        var userB = Core.Domain.Users.User.CreateWithPassword("b@example.com", "x", "Bea", "Placed");
        userB.Id = $"{CleanerB}-user";
        var employeeB = Core.Domain.Users.Employee.CreateWithUser(userB);
        employeeB.Id = CleanerB;
        order.AddAssignedEmployee(OrderEmployee.Create(order, employeeB));

        var seatA = order.AssignedEmployees.Single(oe => oe.EmployeeId == CleanerA);
        var seatB = order.AssignedEmployees.Single(oe => oe.EmployeeId == CleanerB);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderPhotoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var employee = ValidatorTestHelpers.BuildEmployee(caller, ContractStatus.Approved, withAddress: true);
        _employeeRepository.Setup(r => r.GetByIdAsync(caller, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _acceptanceRepository.Setup(r => r.AnyForSeatAsync(seatA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _acceptanceRepository.Setup(r => r.AnyForSeatAsync(seatB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        return order;
    }

    [Fact]
    public async Task The_Placed_Second_Cleaner_Is_Refused_At_Complete_As_Acceptance_Required()
    {
        ArrangeTwoSeatCrew(CleanerB);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.WorkContractAcceptanceRequired);
    }

    [Fact]
    public async Task The_Cleaner_Who_Took_The_Job_Completes_Without_A_Prompt()
    {
        ArrangeTwoSeatCrew(CleanerA);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Cleaner_Not_On_The_Crew_Answers_The_Assignment_Refusal_First()
    {
        ArrangeTwoSeatCrew(StrangerId);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotAssignedToOrder);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.WorkContractAcceptanceRequired);
    }
}
