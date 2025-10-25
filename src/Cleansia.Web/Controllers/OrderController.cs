using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Abstractions;
using Cleansia.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("CreateOrder")]
    [ProducesResponseType(typeof(CreateOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrder.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<CreateOrder.Response>(result);
    }

    [HttpGet("GetPaged")]
    [Permission(Policy.CanViewPagedOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<OrderListItem>> GetPaged([FromQuery] GetPagedOrders.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetById")]
    [Permission(Policy.CanViewOrderDetail)]
    [ProducesResponseType(typeof(OrderItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById([FromQuery] GetOrderDetails.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<OrderItem>(result);
    }

    [HttpPost("TakeOrder")]
    [Permission(Policy.CanTakeOrder)]
    [ProducesResponseType(typeof(TakeOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TakeOrder([FromBody] TakeOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<TakeOrder.Response>(result);
    }
}