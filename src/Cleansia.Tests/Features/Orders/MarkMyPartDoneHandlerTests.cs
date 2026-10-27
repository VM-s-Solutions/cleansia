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
/// A cleaner on a crew job marks their own part done. Owner ruling 2026-10-26: "each cleaner has to mark
/// on his device that he's done".
/// </summary>
public class MarkMyPartDoneHandlerTests
{
    private const string OrderId = "order-part-done-1";
    private const string EmployeeId = "emp-part-done-1";
    private const string OtherEmployeeId = "emp-part-done-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();

    private static Order OrderAt(OrderStatus status, params string[] assignedEmployeeIds)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, status, maxEmployees: 2);

        foreach (var id in assignedEmployeeIds)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(id, ContractStatus.Approved)));
        }

        return order;
    }

    private static OrderEmployee SeatOf(Order order, string employeeId) =>
        order.AssignedEmployees.Single(oe => oe.EmployeeId == employeeId);

    private void Arrange(Order order, string? callerEmployeeId = EmployeeId)
    {
        _orderRepository.Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerEmployeeId);
    }

    private MarkMyPartDone.Handler Handler() => new(_orderRepository.Object, _accessService.Object);

    [Fact]
    public async Task Only_The_Callers_Own_Seat_Is_Stamped()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        Arrange(order);

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(SeatOf(order, EmployeeId).CompletionConfirmedAt);
        Assert.Null(SeatOf(order, OtherEmployeeId).CompletionConfirmedAt);
        Assert.Equal(SeatOf(order, EmployeeId).CompletionConfirmedAt, result.Value.CompletionConfirmedAt);
        Assert.False(result.Value.CrewDone);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
    }

    [Fact]
    public async Task Marking_Again_Is_A_Success_That_Keeps_The_First_Stamp()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        Arrange(order);

        var first = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);
        var second = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.CompletionConfirmedAt, second.Value.CompletionConfirmedAt);
        Assert.Equal(first.Value.CompletionConfirmedAt, SeatOf(order, EmployeeId).CompletionConfirmedAt);
    }

    [Fact]
    public async Task The_Last_Cleaner_To_Mark_Leaves_The_Crew_Done()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        SeatOf(order, OtherEmployeeId).MarkCompletionConfirmed(DateTime.UtcNow.AddMinutes(-5));
        Arrange(order);

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.CrewDone);
    }

    /// <summary>
    /// The order is not in progress either, and the answer must not say so: someone who is not on the crew
    /// learns nothing about the job.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Not_On_The_Crew_Is_Told_Only_That()
    {
        var order = OrderAt(OrderStatus.Confirmed, OtherEmployeeId);
        Arrange(order);

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.EmployeeNotAssignedToOrder, result.Error!.Message);
        Assert.Null(SeatOf(order, OtherEmployeeId).CompletionConfirmedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task A_Job_That_Is_Not_In_Progress_Is_Refused(OrderStatus status)
    {
        var order = OrderAt(status, EmployeeId, OtherEmployeeId);
        Arrange(order);

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotInProgress, result.Error!.Message);
        Assert.Null(SeatOf(order, EmployeeId).CompletionConfirmedAt);
    }

    [Fact]
    public async Task A_Caller_With_No_Cleaner_Profile_Is_Answered_Not_Found()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        Arrange(order, callerEmployeeId: null);

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.All(order.AssignedEmployees, seat => Assert.Null(seat.CompletionConfirmedAt));
    }
}
