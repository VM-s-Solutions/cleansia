using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A job of two or more cleaners is completed only once every cleaner on it has marked their part done,
/// and then by any of them; a job one cleaner holds still completes on their one tap. Owner ruling
/// 2026-10-26: "each cleaner has to mark on his device that he's done. And then there is a shared button
/// appears that can be clicked by any on them".
/// </summary>
public sealed class CompleteOrderCrewNotDoneTests
{
    private const string OrderId = "order-crew-done";
    private const string CleanerA = "emp-crew-a";
    private const string CleanerB = "emp-crew-b";
    private const string StrangerId = "emp-crew-stranger";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();

    private CompleteOrder.Validator CreateValidator() =>
        new(_orderRepository.Object, _employeeRepository.Object, _orderPhotoRepository.Object, _accessService.Object, _acceptanceRepository.Object);

    /// <summary>A two-seat in-progress card job, paid, with its after photo, and both seats under contract.</summary>
    private Order ArrangeTwoSeatCrew(string caller)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, CleanerA, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 2);
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee(CleanerB, ContractStatus.Approved)));

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderPhotoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var employee = ValidatorTestHelpers.BuildEmployee(caller, ContractStatus.Approved, withAddress: true);
        _employeeRepository.Setup(r => r.GetByIdAsync(caller, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _acceptanceRepository.Setup(r => r.AnyForSeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return order;
    }

    private static void MarkDone(Order order, string employeeId) =>
        order.AssignedEmployees.Single(oe => oe.EmployeeId == employeeId).MarkCompletionConfirmed(DateTime.UtcNow);

    [Theory]
    [InlineData(CleanerA)]
    [InlineData(CleanerB)]
    public async Task One_Cleaner_Done_And_One_Still_Working_Is_Refused(string doneCleaner)
    {
        var order = ArrangeTwoSeatCrew(CleanerA);
        MarkDone(order, doneCleaner);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCrewNotDone);
    }

    [Fact]
    public async Task Every_Cleaner_Done_Lets_Any_Of_Them_Complete()
    {
        var order = ArrangeTwoSeatCrew(CleanerB);
        MarkDone(order, CleanerA);
        MarkDone(order, CleanerB);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Cleaner_Not_On_The_Crew_Is_Not_Told_The_Crew_Is_Not_Done()
    {
        var order = ArrangeTwoSeatCrew(StrangerId);
        MarkDone(order, CleanerA);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.Equal(BusinessErrorMessage.EmployeeNotAssignedToOrder, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Crew_Nobody_Has_Marked_Done_Is_Refused()
    {
        ArrangeTwoSeatCrew(CleanerA);

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.Equal(BusinessErrorMessage.OrderCrewNotDone, Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>The owner was told this default and did not veto it: a solo job stays one tap.</summary>
    [Fact]
    public async Task A_Job_One_Cleaner_Holds_Completes_On_Their_One_Tap()
    {
        ArrangeTwoSeatCrew(CleanerA);
        var solo = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, CleanerA, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 2);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { solo }.AsQueryable().BuildMock());

        var result = await CreateValidator().ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
