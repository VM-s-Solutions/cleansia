using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
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
    private const string ThirdEmployeeId = "emp-part-done-3";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();

    private static Order OrderAt(OrderStatus status, params string[] assignedEmployeeIds)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, status, maxEmployees: 3);

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

    private MarkMyPartDone.Handler Handler() =>
        new(_orderRepository.Object, _accessService.Object, _notificationProducer.Object);

    private void VerifyToldThatACrewMateIsDone(string employeeId, Order order, Times times) =>
        _notificationProducer.Verify(
            n => n.NotifyAsync(
                employeeId + "-user",
                NotificationEventCatalog.OrderCrewMateDone,
                It.Is<Dictionary<string, string>>(args =>
                    args.Count == 2
                    && args["orderId"] == OrderId
                    && args["orderNumber"] == order.DisplayOrderNumber),
                order.TenantId,
                AssignmentNotificationSubject.For(OrderId, SeatOf(order, EmployeeId).Id),
                It.IsAny<CancellationToken>()),
            times);

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

    /// <summary>
    /// The mark locks the order's row before it reads the crew, so two cleaners marking at once see each
    /// other's mark, and one racing the job's completion reads it. → CrewCloseRaceTests
    /// </summary>
    [Fact]
    public async Task The_Order_Row_Is_Locked_Before_The_Crew_Is_Read()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        Arrange(order);
        var calls = new List<string>();
        _orderRepository.Setup(r => r.LockForCrewChangeAsync(OrderId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("lock"))
            .Returns(Task.CompletedTask);
        _orderRepository.Setup(r => r.GetQueryable())
            .Callback(() => calls.Add("read"))
            .Returns(new[] { order }.AsQueryable().BuildMock());

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["lock", "read"], calls);
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
    /// "The others are told by push": everyone else still on the job hears that this cleaner has finished,
    /// with nothing but the job's number to read and its id to open, under a key that names this mark.
    /// </summary>
    [Fact]
    public async Task The_First_Mark_Tells_Everyone_Else_On_The_Job()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId, ThirdEmployeeId);
        Arrange(order);

        await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        VerifyToldThatACrewMateIsDone(OtherEmployeeId, order, Times.Once());
        VerifyToldThatACrewMateIsDone(ThirdEmployeeId, order, Times.Once());
        VerifyToldThatACrewMateIsDone(EmployeeId, order, Times.Never());
        _notificationProducer.VerifyNoOtherCalls();
    }

    /// <summary>The last cleaner's mark tells the others too: it is what lets any of them complete the job.</summary>
    [Fact]
    public async Task The_Last_Mark_Tells_The_Others_Who_Had_Already_Marked()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        SeatOf(order, OtherEmployeeId).MarkCompletionConfirmed(DateTime.UtcNow.AddMinutes(-5));
        Arrange(order);

        await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        VerifyToldThatACrewMateIsDone(OtherEmployeeId, order, Times.Once());
        _notificationProducer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Marking_Again_Tells_Nobody()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId);
        SeatOf(order, EmployeeId).MarkCompletionConfirmed(DateTime.UtcNow.AddMinutes(-5));
        Arrange(order);

        var result = await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        Assert.True(result.IsSuccess);
        _notificationProducer.VerifyNoOtherCalls();
    }

    /// <summary>
    /// A crew-mate the platform has barred, or whose account is gone, is not told about the work: the
    /// recipient rule every cleaner-facing job reminder uses.
    /// </summary>
    [Fact]
    public async Task A_Crew_Mate_Who_May_Not_Work_Is_Not_Told()
    {
        var order = OrderAt(OrderStatus.InProgress, EmployeeId, OtherEmployeeId, ThirdEmployeeId);
        SeatOf(order, OtherEmployeeId).Employee!.UpdateContractStatus(ContractStatus.Rejected);
        SeatOf(order, ThirdEmployeeId).Employee!.User!.IsActive = false;
        Arrange(order);

        await Handler().Handle(new MarkMyPartDone.Command(OrderId), default);

        _notificationProducer.VerifyNoOtherCalls();
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
