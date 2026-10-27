using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;
using static Cleansia.Tests.Features.Orders.AutoCompleteMarkedDoneOrderHandlerTests;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The sweep on the 15-minute tick selects the crew jobs everyone marked done two hours ago or more, and
/// sends each to its own unit in a scope of its own, so one that fails holds back none of the others.
/// </summary>
public class AutoCompleteMarkedDoneOrdersTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<AutoCompleteMarkedDoneOrder.Command> _sent = [];
    private int _scopes;

    private AutoCompleteMarkedDoneOrders.Handler Handler()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator>(_ =>
        {
            _scopes++;
            return _mediator.Object;
        });
        return new AutoCompleteMarkedDoneOrders.Handler(
            _orderRepository.Object,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoCompleteMarkedDoneOrders.Handler>.Instance);
    }

    private void Listed(params Order[] orders) =>
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant()).Returns(orders.AsQueryable().BuildMock());

    private void UnitAnswers(Func<AutoCompleteMarkedDoneOrder.Command, BusinessResult<AutoCompleteMarkedDoneOrder.Response>> answer) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrder.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<BusinessResult<AutoCompleteMarkedDoneOrder.Response>> request, CancellationToken _) =>
            {
                var command = (AutoCompleteMarkedDoneOrder.Command)request;
                _sent.Add(command);
                return answer(command);
            });

    [Fact]
    public async Task Only_The_Jobs_Whose_Whole_Crew_Marked_Done_Two_Hours_Ago_Are_Sent_Each_From_A_Scope_Of_Its_Own()
    {
        Listed(
            TwoSeatJob(Cutoff.AddMinutes(-60), Cutoff.AddMinutes(-20), orderId: "order-closes-1"),
            TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-1), orderId: "order-closes-2"),
            TwoSeatJob(Cutoff.AddMinutes(-30), null, orderId: "order-still-cleaning"),
            TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(30), orderId: "order-marked-lately"),
            TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), OrderStatus.Completed, orderId: "order-completed"));
        UnitAnswers(_ => BusinessResult.Success(new AutoCompleteMarkedDoneOrder.Response(true)));

        var result = await Handler().Handle(new AutoCompleteMarkedDoneOrders.Command(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AutoCompleteMarkedDoneOrders.Response(Completed: 2, Considered: 2), result.Value);
        Assert.Equal(["order-closes-1", "order-closes-2"], _sent.Select(c => c.OrderId));
        Assert.All(_sent, c => Assert.InRange(c.CutoffUtc, Now.AddMinutes(-121), DateTime.UtcNow.AddMinutes(-120)));
        Assert.Equal(2, _scopes);
    }

    [Fact]
    public async Task A_Job_That_Throws_Or_Fails_Holds_Back_None_Of_The_Others()
    {
        Listed(
            TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), orderId: "order-a"),
            TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), orderId: "order-b"),
            TwoSeatJob(Cutoff.AddMinutes(-30), Cutoff.AddMinutes(-10), orderId: "order-c"));
        UnitAnswers(command => command.OrderId switch
        {
            "order-a" => throw new InvalidOperationException("order-a"),
            "order-b" => BusinessResult.Failure<AutoCompleteMarkedDoneOrder.Response>(
                new Error(nameof(command.OrderId), BusinessErrorMessage.Required)),
            _ => BusinessResult.Success(new AutoCompleteMarkedDoneOrder.Response(true)),
        });

        var result = await Handler().Handle(new AutoCompleteMarkedDoneOrders.Command(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AutoCompleteMarkedDoneOrders.Response(Completed: 1, Considered: 3, Failed: 2), result.Value);
        Assert.Equal(["order-a", "order-b", "order-c"], _sent.Select(c => c.OrderId));
        Assert.Equal(3, _scopes);
    }
}
