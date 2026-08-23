using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The handler's reload, which was the last null-forgiven one on this pair of commands.
///
/// <para>The validator proves the order exists — but with a DIFFERENT query. A handler that trusts the
/// validator's check while running its own load is exactly how a business error becomes a 500 the
/// caller cannot act on, and <see cref="StartOrder"/> had already been fixed for it. This one had not.
/// </para>
/// </summary>
public class NotifyOnTheWayHandlerTests
{
    private const string OrderId = "order-otw-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();

    private NotifyOnTheWay.Handler CreateHandler() =>
        new(_orderRepository.Object, _producer.Object, _liveActivityProducer.Object);

    [Fact]
    public async Task When_HandlerLoad_Returns_Null_Then_OrderNotFound_NoNre()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(new NotifyOnTheWay.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task When_Order_Loads_Then_Advances_To_OnTheWay()
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Confirmed, "emp-1",
            cleaningDateTime: ValidatorTestHelpers.StartableCleaningTime);
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(new NotifyOnTheWay.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.OnTheWay, order.CurrentStatus);
    }
}
