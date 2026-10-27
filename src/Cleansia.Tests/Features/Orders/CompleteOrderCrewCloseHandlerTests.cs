using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Completing takes the order's row lock before it reads the order, so of two cleaners completing at once,
/// or a cleaner and the automatic close, the second reads what the first committed. What the validator
/// decided before the lock is decided again under it. → CrewCloseRaceTests
/// </summary>
public class CompleteOrderCrewCloseHandlerTests
{
    private const string OrderId = "order-crew-close";
    private const string CleanerA = "emp-crew-close-a";
    private const string CleanerB = "emp-crew-close-b";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IReferralService> _referralService = new();

    private CompleteOrder.Handler Handler() => new(
        _orderRepository.Object,
        _pending.Object,
        _notificationProducer.Object,
        _liveActivityProducer.Object,
        _loyaltyService.Object,
        _referralService.Object);

    private static Order TwoSeatJob(OrderStatus status)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, status, CleanerA, PaymentType.Card, PaymentStatus.Paid, maxEmployees: 2, userId: "customer-crew-close");
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee(CleanerB, ContractStatus.Approved)));
        return order;
    }

    private static void MarkDone(Order order, string employeeId) =>
        order.AssignedEmployees.Single(oe => oe.EmployeeId == employeeId).MarkCompletionConfirmed(DateTime.UtcNow);

    private void Arrange(Order order) =>
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

    private void AssertNothingWasDone()
    {
        _pending.VerifyNoOtherCalls();
        _notificationProducer.VerifyNoOtherCalls();
        _liveActivityProducer.VerifyNoOtherCalls();
        _loyaltyService.VerifyNoOtherCalls();
        _referralService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task The_Order_Row_Is_Locked_Before_The_Order_Is_Read()
    {
        var order = TwoSeatJob(OrderStatus.InProgress);
        MarkDone(order, CleanerA);
        MarkDone(order, CleanerB);
        var calls = new List<string>();
        _orderRepository.Setup(r => r.LockForCrewChangeAsync(OrderId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("lock"))
            .Returns(Task.CompletedTask);
        _orderRepository.Setup(r => r.GetQueryable())
            .Callback(() => calls.Add("read"))
            .Returns(new[] { order }.AsQueryable().BuildMock());

        var result = await Handler().Handle(new CompleteOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["lock", "read"], calls);
        Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
    }

    /// <summary>
    /// Both cleaners pressed Complete; this call passed its validator and waited on the row while the other
    /// completed the job. The job is done, so the answer says so, and nothing about the completion runs a
    /// second time.
    /// </summary>
    [Fact]
    public async Task A_Job_A_Crew_Mate_Completed_Meanwhile_Answers_Completed_And_Runs_Nothing_Again()
    {
        var order = TwoSeatJob(OrderStatus.InProgress);
        MarkDone(order, CleanerA);
        MarkDone(order, CleanerB);
        order.CompleteOrder(42);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        var tracks = order.OrderStatusHistory.Count;
        Arrange(order);

        var result = await Handler().Handle(new CompleteOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(OrderStatus.Completed, result.Value.NewStatus);
        Assert.Equal(42, result.Value.ActualCompletionTime);
        Assert.Equal(tracks, order.OrderStatusHistory.Count);
        AssertNothingWasDone();
    }

    /// <summary>
    /// A cleaner took the open seat of a job under way while this call waited: someone on the job has not
    /// marked their part done, so it is not completed.
    /// </summary>
    [Fact]
    public async Task A_Crew_With_A_Cleaner_Who_Has_Not_Marked_Done_Is_Refused_And_Nothing_Is_Done()
    {
        var order = TwoSeatJob(OrderStatus.InProgress);
        MarkDone(order, CleanerA);
        Arrange(order);

        var result = await Handler().Handle(new CompleteOrder.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderCrewNotDone, result.Error!.Message);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        AssertNothingWasDone();
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task A_Job_That_Is_No_Longer_In_Progress_Is_Refused_And_Nothing_Is_Done(OrderStatus status)
    {
        var order = TwoSeatJob(status);
        MarkDone(order, CleanerA);
        MarkDone(order, CleanerB);
        Arrange(order);

        var result = await Handler().Handle(new CompleteOrder.Command(OrderId), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotInProgress, result.Error!.Message);
        Assert.Equal(status, order.CurrentStatus);
        AssertNothingWasDone();
    }

    /// <summary>The owner was told this default and did not veto it: a solo job stays one tap.</summary>
    [Fact]
    public async Task A_Job_One_Cleaner_Holds_Completes_Without_A_Mark()
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, CleanerA, PaymentType.Card, PaymentStatus.Paid, userId: "customer-crew-close");
        Arrange(order);

        var result = await Handler().Handle(new CompleteOrder.Command(OrderId), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
        _pending.Verify(
            p => p.Enqueue(
                QueueNames.CalculateOrderPay,
                It.IsAny<QueueEnvelope<CalculateOrderPayMessage>>(),
                MessageKeys.Pay(OrderId, CleanerA)),
            Times.Once);
    }
}
