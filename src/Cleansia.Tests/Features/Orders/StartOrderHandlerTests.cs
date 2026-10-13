using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Guards the StartOrder handler against the validator/handler load divergence: the
/// validator gates existence via <c>ExistsAsync</c>, while the handler reloads through a different
/// Include-shaped query. When the two disagree (the handler load returns null where the validator
/// passed), the previous <c>order!.StartOrder()</c> null-forgiving dereference threw an NRE that
/// surfaced as a 500. The handler must return a clean <see cref="BusinessErrorMessage.OrderNotFound"/>
/// instead, and still advance a legitimately loaded order to InProgress.
/// </summary>
public class StartOrderHandlerTests
{
    private const string OrderId = "order-start-1";
    private const string UserId = "owner-user";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<ILogger<StartOrder.Handler>> _logger = new();

    private StartOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _pending.Object,
            _producer.Object,
            _liveActivityProducer.Object);

    private Order ArrangeOrder()
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, "emp-1");

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    // RED before the fix: the validator's ExistsAsync passed, but the handler's Include-shaped reload
    // resolves to null (filter/Include divergence). Current code throws NullReferenceException on
    // order!.StartOrder(); the fixed handler returns a clean OrderNotFound business failure.
    [Fact]
    public async Task When_HandlerLoad_Returns_Null_Then_OrderNotFound_NoNre()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(new StartOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task When_Order_Loads_Then_Advances_To_InProgress()
    {
        var order = ArrangeOrder();

        var result = await CreateHandler().Handle(new StartOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.InProgress, result.Value!.NewStatus);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
    }

    [Fact]
    public async Task A_Guest_Booking_Stages_One_Started_Email_For_The_Committed_Order()
    {
        var order = ArrangeOrder();
        await CreateHandler().Handle(new StartOrder.Command(OrderId), CancellationToken.None);
        _pending.Verify(p => p.Enqueue(QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendOrderStatusEmailMessage>>(m => m.Payload.OrderId == order.Id && m.Payload.Status == OrderStatus.InProgress),
            MessageKeys.OrderStatusEmail(order.Id, OrderStatus.InProgress)), Times.Once);
    }
}
